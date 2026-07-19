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
/// Only tracks a slice of chunks at y 0. Useful for when you want to generate
/// a full column of chunks at a time.
/// </summary>
public class ChunkClusterDirectorFlat : IChunkClusterDirector
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
    private Vector2D<int> _centrePosition;
    /// <summary>The centre position of the cluster, snapped to the nearest chunk boundary.</summary>
    public Vector3D<int> CentrePosition => new(_centrePosition.X, 0, _centrePosition.Y);

    /// <summary>All chunks currently tracked by the Director and their last known state.</summary>
    public IEnumerable<Vector3D<int>> Registry => chunkCompleteByPos.Keys.Select(p => new Vector3D<int>(p.X, 0, p.Y));
    public IChunkGenerationPipeline<Vector3D<int>> GenerationPipeline { get; }

    private readonly Dictionary<Vector2D<int>, bool> chunkCompleteByPos = [];
    private List<Vector2D<int>> toAdd = [];
    private HashSet<Vector2D<int>> toAddCompleted = [];
    private HashSet<Vector2D<int>> toRemove = [];
    private readonly SemaphoreSlim semaphore;


    /// <summary> Converts a Vector3D to a Vector2D with the z for the new y position.</summary>
    public static Vector2D<int> FlatPosition(Vector3D<int> pos) => new(pos.X, pos.Z);
    /// <summary> Converts a Vector2D to a Vector3D with the y for the new z position and places y at 0.</summary>
    public static Vector3D<int> RealPosition(Vector2D<int> pos) => new(pos.X, 0, pos.Y);
    private Vector2D<int> ChunkByPos(Vector2D<int> pos) => new
        (
            (int)MathF.Floor((float)pos.X / ChunkLength) * ChunkLength,
            (int)MathF.Floor((float)pos.Y / ChunkLength) * ChunkLength
        );

    /// <summary>
    /// Sets the number of chunks from the centre to the edge of the cluster.
    /// Has no effect if <paramref name="halfLengthInChunks"/> matches the current value.
    /// Queues additions and removals to reflect the new bounds on the next <see cref="ProcessChunks"/> call.
    /// </summary>
    /// <param name="halfLengthInChunks">
    /// Half the total cluster side length in chunks.
    /// A value of 2 produces a 5×5 cluster.
    /// </param>
    public void SetLoadDistance(int halfLengthInChunks)
    {
        if (halfLengthInChunks == HalfLengthInChunks)
            return;
        HalfLengthInChunks = halfLengthInChunks;
        UpdateManagedChunks();
    }

    /// <summary>
    /// Sets the centre of the cluster, snapped to the floored chunk boundary. Y position is ignored.
    /// Has no effect if the snapped position matches the current centre.
    /// Queues additions and removals to reflect the new position on the next <see cref="ProcessChunks"/> call.
    /// </summary>
    /// <param name="centrePosition">The desired centre in world space.</param>
    public void SetCentrePosition(Vector3D<int> centrePosition)
    {
        Vector2D<int> testPos = FlatPosition(centrePosition);
        testPos = ChunkByPos(testPos);
        if (testPos == _centrePosition)
            return;
        _centrePosition = testPos;
        UpdateManagedChunks();
    }

    private void UpdateManagedChunks()
    {
        toAdd.Clear();
        toRemove.Clear();
        toRemove.UnionWith(chunkCompleteByPos.Keys);
        IEnumerable<Vector2D<int>> newChunks = ExpandingSquarePositions();
        foreach (Vector2D<int> chunk in newChunks)
            if (!toRemove.Remove(chunk)) // if chunk didn't exist
                toAdd.Add(chunk);
        toAddCompleted.Clear();
    }

    private IEnumerable<Vector2D<int>> ExpandingSquarePositions()
    {
        for (int i = 0; i <= HalfLengthInChunks; i++)
        {
            for (int x = -i; x <= i; x++)
            {
                if (x == -i || x == i)
                {
                    for (int z = -i; z <= i; z++)
                            yield return new Vector2D<int>(x, z) * ChunkLength + _centrePosition;
                }
                else
                {
                    yield return new Vector2D<int>(x, -i) * ChunkLength + _centrePosition;
                    yield return new Vector2D<int>(x, i) * ChunkLength + _centrePosition;
                }
            }
        }
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
                    Vector2D<int> neighbor = FlatPosition(State.Chunk + CubicNeighborhood.MooreNeighborhood[rootD] * ChunkLength);
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
                        Vector2D<int> nieghborNeighbor = neighbor + FlatPosition(CubicNeighborhood.MooreNeighborhood[neighborD] * ChunkLength);
                        if (nieghborNeighbor == FlatPosition(State.Chunk))
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
                        neighborsCullable.Add(RealPosition(neighbor));
                }
                // Shouldn't be storing cullable states as they will become stale.
                chunkCompleteByPos[FlatPosition(State.Chunk)] = true;
                if (!handler.OnGenerationComplete(State.Chunk, cullable, [.. neighborsCullable]))
                    return;
                break;
            default:
                throw new NotSupportedException();
        }

        HashSet<Vector3D<int>> generating = [.. GenerationPipeline.ChunksInPipeline.Select(gen => gen.Chunk)];
        Vector2D<int>[] removing = [.. toRemove];
        foreach (Vector2D<int> chunk in removing)
        {
            if (generating.Contains(RealPosition(chunk)))
                return;
            chunkCompleteByPos.Remove(chunk);
            toRemove.Remove(chunk);
            if (!handler.OnDeactivated(RealPosition(chunk)))
                return;
        }

        if (toAdd.Count == toAddCompleted.Count)
            return;

        for (int i = 0; i < toAdd.Count; i++)
        {
            Vector2D<int> chunk = toAdd[i];
            if (toAddCompleted.Contains(chunk))
                continue;
            Vector3D<int> chunkMinBounds = new(chunk.X, -999, chunk.Y);
            Vector3D<int> chunkMaxBounds = new(chunk.X,  999, chunk.Y);
            if (frustum != null && !MatrixPlanes.IsBoxInFrustum(frustum, (Vector3)chunkMinBounds, (Vector3)chunkMaxBounds))
                continue;
            if (chunkCompleteByPos.ContainsKey(chunk))
                throw new InvalidOperationException($"Chunk {chunk} is already being tracked. This should never happen.");
            if (!semaphore.Wait(0))
                return;
            chunkCompleteByPos[chunk] = false;
            toAddCompleted.Add(chunk);
            GenerationPipeline.StartChunk(RealPosition(chunk));
            if (!handler.OnGenerationUpdate(RealPosition(chunk), 0))
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
    /// A value of 2 produces a 5×5 cluster.
    /// </param>
    /// <param name="centrePosition">The initial centre of the cluster in world space. Y position is ignored.</param>
    /// <param name="maxGenerating">
    /// The maximum number of chunks permitted in the generation pipeline simultaneously.
    /// </param>
    public ChunkClusterDirectorFlat
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
        _centrePosition = new(centrePosition.X, centrePosition.Z);
        semaphore = new(maxGenerating);
        UpdateManagedChunks();
    }
}