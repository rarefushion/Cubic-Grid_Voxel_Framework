using GalensUnified.CubicGrid.Core.Math;
using Silk.NET.Maths;

namespace GalensUnified.CubicGrid.Framework;

using ChunkGenState = ChunkGenerationState<Vector3D<int>>;

/// <summary>
/// Tracks which chunks are active within a bounded cluster region, automatically evicting
/// out-of-bounds chunks and scheduling new ones for generation as the centre position or
/// load distance changes.
/// </summary>
public class ChunkClusterDirector : IChunkClusterDirector
{
    /// <summary>
    /// True if any chunk is in the generation pipeline or if unapplied additions
    /// or removals are waiting for <see cref="ProcessChunks"/> to be called.
    /// </summary>
    public bool IsProcessing =>
        toAdd.Count > 0 ||
        toRemove.Count > 0 ||
        GenerationPipeline.ChunksInPipeline.Count() > 0;
    public int ChunkLength { get; }
    /// <summary>The number of chunks from the centre to the edge of the cluster.</summary>
    public int HalfLengthInChunks { get; private set; }
    /// <summary>The centre position of the cluster, snapped to the nearest chunk boundary.</summary>
    public Vector3D<int> CentrePosition { get; private set; }

    /// <summary>All chunks currently tracked by the Director and their last known state.</summary>
    public IEnumerable<ChunkDirectorUpdate> Registry => chunkByPos.Values;
    public IChunkGenerationPipeline<Vector3D<int>> GenerationPipeline { get; }

    private readonly Dictionary<Vector3D<int>, ChunkDirectorUpdate> chunkByPos = [];
    private Queue<Vector3D<int>> toAdd = [];
    private HashSet<Vector3D<int>> toRemove = [];
    private readonly SemaphoreSlim semaphore;

    /// <summary>
    /// Converts a world-space position to the origin of the chunk that contains it.
    /// </summary>
    /// <param name="pos">Any world-space position.</param>
    /// <returns>The chunk-aligned origin of the chunk containing <paramref name="pos"/>.</returns>
    public Vector3D<int> ChunkByPos(Vector3D<int> pos) => new
        (
            (int)MathF.Floor((float)pos.X / ChunkLength) * ChunkLength,
            (int)MathF.Floor((float)pos.Y / ChunkLength) * ChunkLength,
            (int)MathF.Floor((float)pos.Z / ChunkLength) * ChunkLength
        );

    /// <summary>
    /// Sets the number of chunks from the centre to the edge of the cluster.
    /// Has no effect if <paramref name="halfLengthInChunks"/> matches the current value.
    /// Queues additions and removals to reflect the new bounds on the next <see cref="ProcessChunks"/> call.
    /// </summary>
    /// <param name="halfLengthInChunks">
    /// Half the total cluster side length in chunks.
    /// A value of 2 produces a 5×5×5 cluster.
    /// </param>
    public void SetLoadDistance(int halfLengthInChunks)
    {
        if (halfLengthInChunks == HalfLengthInChunks)
            return;
        HalfLengthInChunks = halfLengthInChunks;
        UpdateManagedChunks();
    }

    /// <summary>
    /// Sets the centre of the cluster, snapped to the floored chunk boundary.
    /// Has no effect if the snapped position matches the current centre.
    /// Queues additions and removals to reflect the new position on the next <see cref="ProcessChunks"/> call.
    /// </summary>
    /// <param name="centrePosition">The desired centre in world space.</param>
    public void SetCentrePosition(Vector3D<int> centrePosition)
    {
        centrePosition = ChunkByPos(centrePosition);
        if (centrePosition == CentrePosition)
            return;
        CentrePosition = centrePosition;
        UpdateManagedChunks();
    }

    private void UpdateManagedChunks()
    {
        toAdd.Clear();
        toRemove.Clear();
        toRemove.UnionWith(chunkByPos.Keys);
        IEnumerable<Vector3D<int>> newChunks = CubicNeighborhood.ExpandingCubePositions
            (
                CentrePosition,
                new(HalfLengthInChunks * ChunkLength),
                ChunkLength
            );
        foreach (Vector3D<int> chunk in newChunks)
            if (!toRemove.Remove(chunk))
                toAdd.Enqueue(chunk); // if chunk didn't exist
    }

    /// <summary>
    /// Advances the cluster one step: progresses the generation pipeline,
    /// evicts out-of-bounds chunks, then starts newly in-bounds chunks up to the concurrency limit.
    /// Removals are always processed before additions.
    /// </summary>
    /// <returns>
    /// A lazily evaluated sequence of <see cref="ChunkDirectorUpdate"/> values representing
    /// each state change in the order it occurred. Out-of-bounds evictions are yielded before
    /// new chunk activations. Enumeration may be interrupted between yields to spread work across frames.
    /// </returns>
    public IEnumerable<ChunkDirectorUpdate> ProcessChunks()
    {
        if (!IsProcessing)
            yield break;

        foreach (ChunkGenState chunkState in GenerationPipeline.ProcessChunks())
        switch (chunkState)
        {
            case ChunkGenState.Processing chunk:
                yield return chunkByPos[chunk.Chunk] = (ChunkDirectorUpdate.Generating)chunkByPos[chunk.Chunk] with
                {
                    Stage = chunk.Stage,
                };
                break;
            case ChunkGenState.Finalized chunk:
                semaphore.Release();
                yield return chunkByPos[chunk.Chunk] = new ChunkDirectorUpdate.GenerationComplete(chunk.Chunk);
                break;
            default:
                throw new NotSupportedException();
        }

        HashSet<Vector3D<int>> generating = [.. GenerationPipeline.ChunksInPipeline.Select(gen => gen.Chunk)];
        Vector3D<int>[] removing = [.. toRemove];
        foreach (Vector3D<int> chunk in removing)
        {
            if (generating.Contains(chunk))
                yield break;
            chunkByPos.Remove(chunk);
            toRemove.Remove(chunk);
            yield return new ChunkDirectorUpdate.Deactivated(chunk);
        }

        while (toAdd.Count > 0 && semaphore.Wait(0))
        {
            Vector3D<int> chunk = toAdd.Dequeue();
            if (chunkByPos.ContainsKey(chunk))
                throw new InvalidOperationException($"Chunk {chunk} is already being tracked. This should never happen.");
            chunkByPos[chunk] = new ChunkDirectorUpdate.Generating(chunk, 0);
            GenerationPipeline.StartChunk(chunk);
            yield return chunkByPos[chunk];
        }
    }

    /// <summary>
    /// Initialises the Director and queues the initial set of chunks for generation.
    /// </summary>
    /// <param name="generationPipeline">The pipeline chunks are submitted to for staged generation.</param>
    /// <param name="chunkLength">The side length of a single chunk in world units.</param>
    /// <param name="clusterHalfLengthInChunks">
    /// Half the total cluster side length in chunks.
    /// A value of 2 produces a 5×5×5 cluster.
    /// </param>
    /// <param name="centrePosition">The initial centre of the cluster in world space.</param>
    /// <param name="maxGenerating">
    /// The maximum number of chunks permitted in the generation pipeline simultaneously.
    /// </param>
    public ChunkClusterDirector
    (
        IChunkGenerationPipeline<Vector3D<int>> generationPipeline,
        int chunkLength,
        int clusterHalfLengthInChunks,
        Vector3D<int> centrePosition,
        int maxGenerating
    )
    {
        GenerationPipeline = generationPipeline;
        ChunkLength = chunkLength;
        HalfLengthInChunks = clusterHalfLengthInChunks;
        CentrePosition = centrePosition;
        semaphore = new(maxGenerating);
        UpdateManagedChunks();
    }
}