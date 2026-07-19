using System.Numerics;
using GalensUnified.CubicGrid.Core.Math;
using GalensUnified.CubicGrid.Renderer.NET;
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
    public int HalfHeightInChunks { get; private set; }
    /// <summary>The centre position of the cluster, snapped to the nearest chunk boundary.</summary>
    public Vector3D<int> CentrePosition { get; private set; }

    /// <summary>All chunks currently tracked by the Director and their last known state.</summary>
    public IEnumerable<Vector3D<int>> Registry => chunkCompleteByPos.Keys;
    public IChunkGenerationPipeline<Vector3D<int>> GenerationPipeline { get; }

    private readonly Dictionary<Vector3D<int>, bool> chunkCompleteByPos = [];
    private List<Vector3D<int>> toAdd = [];
    private HashSet<Vector3D<int>> toAddCompleted = [];
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
        toRemove.UnionWith(chunkCompleteByPos.Keys);
        IEnumerable<Vector3D<int>> newChunks = CubicNeighborhood.ExpandingCubePositions
            (
                CentrePosition,
                new(HalfLengthInChunks * ChunkLength, HalfHeightInChunks * ChunkLength, HalfLengthInChunks * ChunkLength),
                ChunkLength
            );
        HashSet<Vector3D<int>> Added = [];
        foreach (Vector3D<int> chunk in newChunks)
            if (!toRemove.Remove(chunk)) // if chunk didn't exist
                if (Added.Add(chunk) && !chunkCompleteByPos.ContainsKey(chunk)) // ExpanddingCubePositions can duplicate positions. Need to refactor it.
                    toAdd.Add(chunk);
        toAddCompleted.Clear();
    }

    /// <summary>
    /// Advances the generation pipeline, invoking <paramref name="handler"/> on updates.
    /// evicts out-of-bounds chunks, then starts newly in-bounds chunks up to the concurrency limit.
    /// Removals are always processed before additions.
    /// </summary>
    public void ProcessChunks<THandler>(THandler handler, MatrixPlanes.Plane[]? frustum = null) where THandler : struct, IChunkDirectorUpdateHandler
    {
        if (!IsProcessing)
            return;

        foreach (ChunkGenState chunkState in GenerationPipeline.ProcessChunks())
        switch (chunkState)
        {
            case ChunkGenState.Processing chunk:
                if (!handler.OnGenerationUpdate(chunk.Chunk, chunk.Stage))
                    return;
                break;
            case ChunkGenState.Finalized State:
                semaphore.Release();
                List<Vector3D<int>> neighborsCullable = [];
                // Is the finalized chunk cullable
                bool cullable = true;
                for (int rootD = 0; rootD < 6; rootD++)
                {
                    // First 6 of MooreNeighborhood are faces.
                    Vector3D<int> neighbor = State.Chunk + (CubicNeighborhood.MooreNeighborhood[rootD] * ChunkLength);
                    if
                    (
                        !chunkCompleteByPos.TryGetValue(neighbor, out bool NComplete) ||
                        !NComplete
                    )
                    {
                        cullable = false;
                        continue;
                    }
                    // Is neighbor cullable
                    bool neighborCullable = true;
                    for (int neighborD = 0; neighborD < 6; neighborD++)
                    {
                        Vector3D<int> nieghborNeighbor = neighbor + (CubicNeighborhood.MooreNeighborhood[neighborD] * ChunkLength);
                        if (nieghborNeighbor == State.Chunk)
                            continue;
                        if
                        (
                            !chunkCompleteByPos.TryGetValue(nieghborNeighbor, out bool NNComplete) ||
                            !NNComplete
                        )
                        {
                            neighborCullable = false;
                            break;
                        }
                    }
                    if (neighborCullable)
                        neighborsCullable.Add(neighbor);
                }
                // Shouldn't be storing cullable states as they will become stale.
                chunkCompleteByPos[State.Chunk] = true;
                if (!handler.OnGenerationComplete(State.Chunk, cullable, [.. neighborsCullable]))
                    return;
                break;
            default:
                throw new NotSupportedException();
        }

        HashSet<Vector3D<int>> generating = [.. GenerationPipeline.ChunksInPipeline.Select(gen => gen.Chunk)];
        Vector3D<int>[] removing = [.. toRemove];
        foreach (Vector3D<int> chunk in removing)
        {
            if (generating.Contains(chunk))
                return;
            chunkCompleteByPos.Remove(chunk);
            toRemove.Remove(chunk);
            if (!handler.OnDeactivated(chunk))
                return;
        }

        if (toAdd.Count == toAddCompleted.Count)
            return;

        for (int i = 0; i < toAdd.Count; i++)
        {
            Vector3D<int> chunk = toAdd[i];
            if (toAddCompleted.Contains(chunk))
                continue;
            Vector3D<int> chunkMaxBounds = chunk + Vector3D<int>.One * ChunkLength;
            if (frustum != null && !MatrixPlanes.IsBoxInFrustum(frustum, (Vector3)chunk, (Vector3)chunkMaxBounds))
                continue;
            if (chunkCompleteByPos.ContainsKey(chunk))
                throw new InvalidOperationException($"Chunk {chunk} is already being tracked. This should never happen.");
            if (!semaphore.Wait(0))
                return;
            chunkCompleteByPos[chunk] = false;
            toAddCompleted.Add(chunk);
            GenerationPipeline.StartChunk(chunk);
            if (!handler.OnGenerationUpdate(chunk, 0))
                return;
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
        int clusterHalfHeightInChunks,
        Vector3D<int> centrePosition,
        int maxGenerating
    )
    {
        GenerationPipeline = generationPipeline;
        ChunkLength = chunkLength;
        HalfLengthInChunks = clusterHalfLengthInChunks;
        HalfHeightInChunks = clusterHalfHeightInChunks;
        CentrePosition = centrePosition;
        semaphore = new(maxGenerating);
        UpdateManagedChunks();
    }
}