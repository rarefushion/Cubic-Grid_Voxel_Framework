using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Framework;
using GalensUnified.CubicGrid.Framework.ChunkOrchestration;
using Silk.NET.Maths;

public struct ChunkDirectorHandler<TChunkDims>
(
    ChunkCluster<TChunkDims> chunkCluster,
    ChunkProcessor<TChunkDims> processor
) : IChunkDirectorUpdateHandler
where TChunkDims : IChunkDims
{
    private readonly ChunkCluster<TChunkDims> chunkCluster = chunkCluster;
    private readonly ChunkProcessor<TChunkDims> processor = processor;

    public bool OnDeactivated(Vector3D<int> Chunk)
    {
        // Neighbors to this chunk will have holes if they were culled.
        processor.Deactivate(Chunk);
        return !Program.OverTargtetFrameTime();
    }

    public bool OnGenerationComplete(Vector3D<int> Chunk, bool Cullable, Vector3D<int>[] CullNeighbors)
    {
        if (Cullable)
            processor.CullReRender(Chunk);
        foreach (Vector3D<int> neighbor in CullNeighbors)
            processor.CullReRender(neighbor);
        return !Program.OverTargtetFrameTime();
    }

    public bool OnGenerationUpdate(Vector3D<int> Chunk, int Stage) => true;
}