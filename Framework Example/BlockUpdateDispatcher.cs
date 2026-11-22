using System.Numerics;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Core.Math;
using GalensUnified.CubicGrid.Framework;
using Silk.NET.Maths;

/// <summary>Contains the logic for how to handle block updates.</summary>
public class BlockUpdateDispatcher<TChunkDims>
(
    ChunkCluster<TChunkDims> chunkCluster,
    ChunkMeshManager<TChunkDims> chunkMeshManager,
    Vector3 sunDirection
)
where TChunkDims : IChunkDims
{
    private readonly ChunkCluster<TChunkDims> chunkCluster = chunkCluster;
    private readonly ChunkMeshManager<TChunkDims> chunkMeshManager = chunkMeshManager;
    private readonly Vector3 sunDirection = sunDirection;

    public void BlockUpdated(Vector3D<int> blockPos)
    {
        Vector3D<int> chunkPos = ChunkMath<TChunkDims>.FloorToChunk(blockPos);
        // chunksUpdated ensures chunks aren't updated more than once
        // the base chunkPos will be updated once all the neighbors have been updated
        HashSet<Vector3D<int>> chunksUpdated = [ chunkPos ];
        // Update Block Neighbors
        for (Direction d = 0; d < (Direction)6; d++)
        {
            Vector3D<int> testPosition = blockPos + d.ToVector().Floor();
            chunkCluster.TryUpdateBlockData(testPosition, new ChunkCluster<TChunkDims>.BlockUpdate<string>("Neighbor Updated"));
            Vector3D<int> testChunkPosition = ChunkMath<TChunkDims>.FloorToChunk(testPosition);
            if (chunkCluster.IsActive(testChunkPosition) && chunksUpdated.Add(testChunkPosition))
                chunkMeshManager.RenderSynchronous(testChunkPosition);
        }
        // Update Modified Chunk
        chunkMeshManager.RenderSynchronous(chunkPos);
        // Update Sun Shadows
        foreach (Direction direction in Enum.GetValues(typeof(Direction)))
        {
            if
            (
                ShapeInstancer<TChunkDims>.IsLightOccluded
                (
                    (Vector3)blockPos, direction,
                    0, chunkCluster,
                    sunDirection, float.PositiveInfinity,
                    out _,
                    out Raycasting.RaycastResult? testRayResult
                ) &&
                testRayResult != null
            )
            {
                Vector3D<int> testChunk = ChunkMath<TChunkDims>.FloorToChunk(testRayResult!.BlockPosition);
                if (testRayResult!.Block != 0 && chunksUpdated.Add(testChunk))
                    chunkMeshManager.RenderAsync(testChunk);
            }
        }
        // Update Sky Shadows
        foreach (Direction direction in Enum.GetValues(typeof(Direction)))
        {
            if
            (
                ShapeInstancer<TChunkDims>.IsLightOccluded
                (
                    (Vector3)blockPos, direction,
                    0, chunkCluster,
                    -Vector3.UnitY, ShapeInstancer<TChunkDims>.maxSkyShadeDisWithSun,
                    out _,
                    out Raycasting.RaycastResult? testRayResult
                ) &&
                testRayResult != null
            )
            {
                Vector3D<int> testChunk = ChunkMath<TChunkDims>.FloorToChunk(testRayResult!.BlockPosition);
                if (testRayResult!.Block != 0 && chunksUpdated.Add(testChunk))
                    chunkMeshManager.RenderAsync(testChunk);
            }
        }
    }
}