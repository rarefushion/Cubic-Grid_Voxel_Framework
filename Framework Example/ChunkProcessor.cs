using System.Numerics;
using GalensUnified.CubicGrid.Framework;
using GalensUnified.CubicGrid.Renderer.NET;
using Silk.NET.Maths;

using static BlockIDs;

public enum ChunkGenerationStage
{
    CalculatingPoints,
    Rendering
}

public class ChunkProcessor(ChunkCluster cluster, Shader shader, Vector3 sunDirection, float skyOccludedShade, float sunOccludedShade) : IChunkProcessor<Vector3D<int>>
{
    private readonly int chunkLength = cluster.chunkLength;
    private readonly Vector3 sun = sunDirection;

    private readonly ChunkCluster cluster = cluster;
    private readonly Shader shader = shader;
    private static readonly FastNoiseLite FNL;

    /// <summary>Gets the center of a face of a cube using the standardized order: -z, +z, +y, -y, -x then +x.</summary>
    public static readonly Vector3[] FaceCenters =
    [
        new(0.5f, 0.5f, 0.0f),
        new(0.5f, 0.5f, 1.0f),
        new(0.5f, 1.0f, 0.5f),
        new(0.5f, 0.0f, 0.5f),
        new(0.0f, 0.5f, 0.5f),
        new(1.0f, 0.5f, 0.5f),
    ];

    public ChunkTaskGate GetChunkTaskGate(Vector3D<int> chunk, int nextStage) => (ChunkGenerationStage)nextStage switch
    {
        ChunkGenerationStage.CalculatingPoints => new ChunkTaskGate.Proceed(),
        ChunkGenerationStage.Rendering => new ChunkTaskGate.Proceed(),
        _ => new ChunkTaskGate.Halt.Complete()
    };

    public ChunkTaskType GetChunkTask(Vector3D<int> chunk, int stage) => (ChunkGenerationStage)stage switch
    {
        ChunkGenerationStage.CalculatingPoints => new ChunkTaskType.Async<Vector3D<int>>(CalculatePointsAsync),
        ChunkGenerationStage.Rendering => new ChunkTaskType.Synchronous<Vector3D<int>>(RenderTask),
        _ => throw new Exception($"Stage '{stage}' doesn't exist.")
    };

    public async Task CalculatePointsAsync(Vector3D<int> chunk, int stage)
    {
        Span<ushort> blocks = cluster.GetChunkByPosition(chunk);
        for (int blockZ = 0; blockZ < chunkLength; blockZ++)
        for (int blockX = 0; blockX < chunkLength; blockX++)
        for (int blockY = 0; blockY < chunkLength; blockY++)
        {
            Vector3D<int> blockPos = new Vector3D<int>(blockX, blockY, blockZ) + chunk;
            float errosion = FNL.GetNoise(blockPos.X, blockPos.Y, blockPos.Z);
            // Doesn't use Y(height) so the value is the same regardless of height.
            float mountainous = (FNL.GetNoise(blockPos.X, blockPos.Z) + 1) / 2;
            int mountainHeight = (int)(mountainous * Program.mountainHeight);
            int i = (blockZ * chunkLength + blockY) * chunkLength + blockX;
            if (blockPos.Y > mountainHeight)
                blocks[i] = Air;
            else if (blockPos.Y == mountainHeight)
                blocks[i] = Grass;
            else if (blockPos.Y > mountainHeight - 5)
                blocks[i] = Dirt;
            else
                blocks[i] = Stone;
            blocks[i] = (Math.Abs(blockPos.X) % cluster.chunkLength == 0 && blocks[i] == Grass) ? Dirt : blocks[i];
            blocks[i] = (Math.Abs(blockPos.Z) % cluster.chunkLength == 0 && blocks[i] == Grass) ? Dirt : blocks[i];
            if (errosion > 0.5f)
                blocks[i] = Air;
        }
    }

    public Task RenderTask(Vector3D<int> chunk, int stage)
    {
        BlockInstance[] blocks = BlockCulling.CullSingleChunk(cluster.GetChunkByPosition(chunk), chunkLength);
        blocks = ShadeBlocks(blocks, chunk);
        shader.RenderChunk((Vector3)chunk, blocks);
        return Task.CompletedTask;
    }

    public void CullReRender(Vector3D<int> chunk)
    {
        // Get neighbors
        Span<ushort> negZChunk = cluster.GetChunkByPosition(chunk + Program.BlockPosByVector3(BlockCulling.directions[0] * chunkLength));
        Span<ushort> posZChunk = cluster.GetChunkByPosition(chunk + Program.BlockPosByVector3(BlockCulling.directions[1] * chunkLength));
        Span<ushort> posYChunk = cluster.GetChunkByPosition(chunk + Program.BlockPosByVector3(BlockCulling.directions[2] * chunkLength));
        Span<ushort> negYChunk = cluster.GetChunkByPosition(chunk + Program.BlockPosByVector3(BlockCulling.directions[3] * chunkLength));
        Span<ushort> negXChunk = cluster.GetChunkByPosition(chunk + Program.BlockPosByVector3(BlockCulling.directions[4] * chunkLength));
        Span<ushort> posXChunk = cluster.GetChunkByPosition(chunk + Program.BlockPosByVector3(BlockCulling.directions[5] * chunkLength));

        shader.DeactivateChunk((Vector3)chunk);
        BlockInstance[] blocks = BlockCulling.CullChunk(cluster.GetChunkByPosition(chunk), chunkLength, negZChunk, posZChunk, posYChunk, negYChunk, negXChunk, posXChunk);
        blocks = ShadeBlocks(blocks, chunk);
        shader.RenderChunk((Vector3)chunk, blocks);
    }

    public BlockInstance[] ShadeBlocks(BlockInstance[] blocks, Vector3D<int> chunk)
    {
        for (int b = 0; b < blocks.Length; b++)
        {
            float[] shading = [1,1,1,1,1,1];
            for (int f = 0; f < 6; f++)
            {
                if (cluster.Raycast((Vector3)chunk + blocks[b].position + FaceCenters[f], -sun).Block != Air)
                    shading[f] -= sunOccludedShade;
                if (cluster.Raycast((Vector3)chunk + blocks[b].position + FaceCenters[f], Vector3.UnitY).Block != Air)
                    shading[f] -= skyOccludedShade;
            }
            blocks[b] = new(blocks[b].position, blocks[b].block, shading);
        }
        return blocks;
    }

    static ChunkProcessor()
    {
        FNL = new(Program.seed);
        FNL.SetFrequency(Program.worldScale);
    }
}