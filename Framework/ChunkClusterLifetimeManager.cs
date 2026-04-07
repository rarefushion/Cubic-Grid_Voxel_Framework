using System.Numerics;
using Silk.NET.Maths;

namespace GalensUnified.CubicGrid.Framework;

/// <summary>
/// Manages the multi-stage generation and unloading pipeline for a cluster of chunks.
/// IChunkLoadRegion and IChunkClusterGenerationManager are routed together.
/// Updating load region, signaling chunk removal and processing chunks.
/// </summary>
public class ChunkClusterLifetimeManager
{
    /// <summary>Will be called when a chunk has been removed.</summary>
    public event Action<Vector3D<int>>? RemoveChunk;
    /// <summary>Chunks that in queue to be removed.</summary>
    public readonly List<Vector3D<int>> chunksToDestroy = [];
    public readonly IChunkClusterRegistry loadedRegion;
    public readonly IChunkClusterGenerationManager generationManager;

    private readonly int chunkLength;
    private Vector3D<int> previousChunkPosition;

    /// <summary>
    /// Performs one full pass of the lifetime pipeline: Updates loaded chunks, destroys chunks and processes new chunks.
    /// Does not process new chunks unless all chunks in chunksToDestroy have been removed.
    /// Returns early at any point if <paramref name="cancelationCondition"/> becomes <c>true</c>.
    /// </summary>
    public void ProcessChunks(Vector3D<int> currentPosition, CancelationCondition cancelationCondition)
    {
        if (cancelationCondition())
            return;

        Vector3D<int> chunkPos = ChunkByPos(currentPosition);
        if (chunkPos != previousChunkPosition)
        {
            loadedRegion.SetPosition(chunkPos);
            previousChunkPosition = chunkPos;
        }

        if (DestroyedChunks(cancelationCondition))
            generationManager.ProcessChunks(cancelationCondition);
    }

    private Vector3D<int> ChunkByPos(Vector3D<int> pos) => new
        (
            (int)MathF.Floor(pos.X / chunkLength) * chunkLength,
            (int)MathF.Floor(pos.Y / chunkLength) * chunkLength,
            (int)MathF.Floor(pos.Z / chunkLength) * chunkLength
        );

    private bool DestroyedChunks(CancelationCondition cancelationCondition)
    {
        for (int i = chunksToDestroy.Count - 1; i >= 0; i--)
        {
            if (cancelationCondition())
                return false;
            Vector3D<int> chunk = chunksToDestroy[0];
            RemoveChunk?.Invoke(chunk);
            chunksToDestroy.Remove(chunk);
        }
        return chunksToDestroy.Count == 0;
    }

    public ChunkClusterLifetimeManager(int chunkLength, IChunkClusterRegistry loadedRegion, IChunkClusterGenerationManager generationManager)
    {
        this.chunkLength = chunkLength;
        this.loadedRegion = loadedRegion;
        this.generationManager = generationManager;

        this.loadedRegion.ChunkRemoved += chunksToDestroy.Add;
        this.loadedRegion.ChunkRemoved += generationManager.DiscardChunk;
        this.loadedRegion.ChunkAdded += generationManager.EnqueueChunk;
        foreach (Vector3D<int> chunk in loadedRegion.GetLoadedChunks())
            generationManager.EnqueueChunk(chunk);
    }
}