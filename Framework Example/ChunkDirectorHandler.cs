using System.Collections.Concurrent;
using System.Numerics;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Framework;
using GalensUnified.CubicGrid.Framework.ChunkOrchestration;
using Silk.NET.Maths;

public struct ChunkDirectorHandler<TChunkDims>
(
    ChunkCluster<TChunkDims> chunkCluster,
    ChunkProcessor<TChunkDims> processor,
    ChunkMeshManager<TChunkDims> meshManager
) : IChunkDirectorUpdateHandler
where TChunkDims : IChunkDims
{
    private readonly ChunkCluster<TChunkDims> chunkCluster = chunkCluster;
    private readonly ChunkProcessor<TChunkDims> processor = processor;
    private readonly ChunkMeshManager<TChunkDims> meshManager = meshManager;

    public readonly bool OnDeactivated(Vector3D<int> Chunk)
    {
        // Neighbors to this chunk will have holes if they were culled.
        if (!Program.lockGenerationHeight)
        {
            chunkCluster.TryRemoveChunk(Chunk);
            meshManager.DeactivateChunk((Vector3)Chunk);
        }
        else
            for (int y = ChunkProcessor<TChunkDims>.LowestPointInChunks; y < ChunkProcessor<TChunkDims>.HeighestPointInChunks; y++)
            {
                Chunk.Y = y * TChunkDims.Length;
                chunkCluster.TryRemoveChunk(Chunk);
                meshManager.DeactivateChunk((Vector3)Chunk);
            }
        return !Program.OverTargtetFrameTime();
    }

    public readonly bool OnGenerationComplete(Vector3D<int> Chunk, bool Cullable, Vector3D<int>[] CullNeighbors)
    {
        ChunkProcessor<TChunkDims> processor = this.processor;
        if (!Program.lockGenerationHeight)
        {
            if (!Cullable)
                meshManager.RenderAsync(Chunk);
            else
            {
                ConcurrentQueue<Action> queue = processor.NeedsProcessingByChunk.GetOrAdd(Chunk, _ => []);
                queue.Enqueue(() => processor.UpdateBlockBehaviors(Chunk));
            }
            foreach (Vector3D<int> neighbor in CullNeighbors)
            {
                ConcurrentQueue<Action> queue = processor.NeedsProcessingByChunk.GetOrAdd(neighbor, _ => []);
                queue.Enqueue(() => processor.UpdateBlockBehaviors(neighbor));
            }
        }
        else
            for (int y = ChunkProcessor<TChunkDims>.LowestPointInChunks; y < ChunkProcessor<TChunkDims>.HeighestPointInChunks; y++)
            {
                Chunk.Y = y * TChunkDims.Length;
                if (!Cullable)
                    meshManager.RenderAsync(Chunk);
                else
                {
                    ConcurrentQueue<Action> queue = processor.NeedsProcessingByChunk.GetOrAdd(Chunk, _ => []);
                    queue.Enqueue(() => processor.UpdateBlockBehaviors(Chunk));
                }
                foreach (Vector3D<int> neighbor in CullNeighbors)
                {
                    ConcurrentQueue<Action> queue = processor.NeedsProcessingByChunk.GetOrAdd(neighbor, _ => []);
                    queue.Enqueue(() => processor.UpdateBlockBehaviors(neighbor));
                }
            }
        return !Program.OverTargtetFrameTime();
    }

    public readonly bool OnGenerationUpdate(Vector3D<int> Chunk, int Stage) => true;
}