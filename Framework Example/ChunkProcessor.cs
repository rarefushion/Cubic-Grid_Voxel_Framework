using System.Numerics;
using GalensUnified.CubicGrid.Framework;
using GalensUnified.CubicGrid.Renderer.NET;
using Silk.NET.Maths;

using static BlockIDs;

public class ChunkProcessor(ChunkCluster cluster, Shader shader) : IChunkProcessor
{
    private readonly int chunkLength = cluster.chunkLength;

    private readonly ChunkCluster cluster = cluster;
    private readonly Shader shader = shader;

    public async Task CalculatePointsAsync(Vector3D<int> chunk)
    {
        Span<ushort> blocks = cluster.GetChunkByPosition(chunk);
        for (int blockZ = 0; blockZ < chunkLength; blockZ++)
        for (int blockX = 0; blockX < chunkLength; blockX++)
        for (int blockY = 0; blockY < chunkLength; blockY++)
        {
            Vector3D<int> blockPos = new Vector3D<int>(blockX, blockY, blockZ) + chunk;
            int i = (blockZ * chunkLength + blockY) * chunkLength + blockX;
            blocks[i] = blockPos.Y switch
            {
                > 0 => Air,   // Air above 0
                0 => Grass,   // Grass floor
                -2 => Air,    // Air slice
                > -5 => Dirt, // Dirt between -5 and 0, the soil layer
                -16 => Air,   // Air slice
                -31 => Air,   // Air slice
                -49 => Air,   // Air slice
                _ => Stone,   // Stone default
            };
            blocks[i] = (Math.Abs(blockPos.Z) == blockPos.Y && Math.Abs(blockPos.X) % 10 > 5) ? Grass : blocks[i];
            blocks[i] = (Math.Abs(blockPos.X) == blockPos.Y && Math.Abs(blockPos.Z) % 14 > 7) ? Grass : blocks[i];
            blocks[i] = (Math.Abs(blockPos.X) % cluster.chunkLength == 0 && blocks[i] == Grass) ? Dirt : blocks[i];
            blocks[i] = (Math.Abs(blockPos.Z) % cluster.chunkLength == 0 && blocks[i] == Grass) ? Dirt : blocks[i];
        }
    }

    public async Task OnNeighborPointsCalculatedAsync(Vector3D<int> chunk)
    {
        // Empty for now
    }

    public async Task RenderAsync(Vector3D<int> chunk)
    {
        int worldIndex = cluster.IndexByChunkCoord(cluster.ChunkCoordByGlobalPos(chunk));
        shader.RenderChunk((Vector3)chunk, worldIndex, cluster.GetChunkByPosition(chunk));
    }
}