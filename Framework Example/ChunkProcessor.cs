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

public class ChunkProcessor(ChunkCluster cluster, Shader shader) : IChunkProcessor
{
    private readonly int chunkLength = cluster.chunkLength;

    private readonly ChunkCluster cluster = cluster;
    private readonly Shader shader = shader;
    private static readonly FastNoiseLite FNL;

    public int StagesCount => Enum.GetNames(typeof(ChunkGenerationStage)).Length;

    public bool IsReadyForNextStage(Vector3D<int> chunk, int stage) =>
        true;

    public Task ProcessStage(Vector3D<int> chunk, int stage) => (ChunkGenerationStage)stage switch
    {
        ChunkGenerationStage.CalculatingPoints => CalculatePointsAsync(chunk),
        ChunkGenerationStage.Rendering => RenderAsync(chunk),
        _ => throw new Exception($"Stage '{stage}' doesn't exist.")
    };

    public async Task CalculatePointsAsync(Vector3D<int> chunk)
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

    public Task RenderAsync(Vector3D<int> chunk)
    {
        int worldIndex = cluster.IndexByChunkCoord(cluster.ChunkCoordByGlobalPos(chunk));
        shader.RenderChunk((Vector3)chunk, worldIndex, cluster.GetChunkByPosition(chunk));
        return Task.CompletedTask;
    }

    static ChunkProcessor()
    {
        FNL = new(Program.seed);
        FNL.SetFrequency(Program.worldScale);
    }
}