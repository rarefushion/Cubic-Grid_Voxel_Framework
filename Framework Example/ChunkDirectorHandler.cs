using System.Numerics;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Framework;
using GalensUnified.CubicGrid.Renderer.NET;
using Silk.NET.Maths;

public struct ChunkDirectorHandler<TChunkDims>
(
    Shader shader,
    ChunkCluster<TChunkDims> chunkCluster,
    ChunkProcessor<TChunkDims> processor
) : IChunkDirectorUpdateHandler
where TChunkDims : IChunkDims
{
    private readonly Shader shader = shader;
    private readonly ChunkCluster<TChunkDims> chunkCluster = chunkCluster;
    private readonly ChunkProcessor<TChunkDims> processor = processor;

    public bool OnDeactivated(Vector3D<int> Chunk)
    {
        // Neighbors to this chunk will have holes if they were culled.
        shader.DeactivateChunk((Vector3)Chunk);
        chunkCluster.TryRemoveChunk(Chunk);
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