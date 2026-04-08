using System.Numerics;
using Silk.NET.Maths;

using ChunkUpdate = GalensUnified.CubicGrid.Framework.IChunkClusterRegistry.ChunkUpdate;

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
    /// <summary>Will be called when a chunk has been Added.</summary>
    public event Action<Vector3D<int>>? AddedChunk;
    /// <summary>Chunks that in queue to be removed.</summary>
    public readonly List<Vector3D<int>> chunksToDestroy = [];
    public readonly IChunkClusterRegistry clusterRegistry;
    public readonly IChunkClusterGenerationManager generationManager;

    private readonly int chunkLength;
    private Vector3D<int> previousChunkPosition;
    private IEnumerator<ChunkUpdate> chunkRegistryUpdater;

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
            chunkRegistryUpdater.Dispose();
            chunkRegistryUpdater = clusterRegistry.SetPosition(chunkPos).GetEnumerator();
            previousChunkPosition = chunkPos;
        }

        ProcessRegistry(cancelationCondition);
        if (!clusterRegistry.IsProcessing)
            generationManager.ProcessChunks(cancelationCondition);
    }

    private Vector3D<int> ChunkByPos(Vector3D<int> pos) => new
        (
            (int)MathF.Floor((float)pos.X / chunkLength) * chunkLength,
            (int)MathF.Floor((float)pos.Y / chunkLength) * chunkLength,
            (int)MathF.Floor((float)pos.Z / chunkLength) * chunkLength
        );

    private void ProcessRegistry(CancelationCondition cancelationCondition)
    {
        while (!cancelationCondition() && chunkRegistryUpdater.MoveNext())
        {
            ChunkUpdate chunkUpdate = chunkRegistryUpdater.Current;
            if (chunkUpdate.IsActive)
            {
                generationManager.EnqueueChunk(chunkUpdate.Position);
                AddedChunk?.Invoke(chunkUpdate.Position);
            }
            else
            {
                generationManager.DiscardChunk(chunkUpdate.Position);
                RemoveChunk?.Invoke(chunkUpdate.Position);
            }
        }
    }

    public ChunkClusterLifetimeManager(int chunkLength, IChunkClusterRegistry clusterRegistry, IChunkClusterGenerationManager generationManager)
    {
        this.chunkLength = chunkLength;
        this.clusterRegistry = clusterRegistry;
        this.generationManager = generationManager;

        previousChunkPosition = Vector3D<int>.Zero;
        chunkRegistryUpdater = clusterRegistry.SetPosition(previousChunkPosition).GetEnumerator();
    }
}