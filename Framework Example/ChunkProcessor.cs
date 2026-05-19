using System.Collections.Concurrent;
using System.Numerics;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Framework;
using GalensUnified.CubicGrid.Renderer.NET;
using Silk.NET.Maths;

using static BlockIDs;
using static GalensUnified.CubicGrid.Core.Raycasting;

public enum ChunkGenerationStage
{
    CalculatingPoints, // Individual blocks
    EnableInCluster, // Tell the Cluster that this chunk is enabled, the cluster's tracker is not concurrent
    CullAndShade // Create the face instances to render
}

public class ChunkProcessor<TChunkDims>
(
    ChunkCluster<TChunkDims> cluster,
    Shader shader,
    Vector3 sunDirection,
    float sunOccludedShade,
    float minBrightness
)
: IChunkProcessor<Vector3D<int>>
where TChunkDims : IChunkDims
{
    private const int maxSkyShadeDisWithSun = 25;
    private const float skyOccludedShade = 0.2f;
    private const float abientOcclusionShade = 0.05f;
    private readonly Vector3 sun = sunDirection;

    private readonly ChunkCluster<TChunkDims> cluster = cluster;
    private readonly Shader shader = shader;
    private static readonly FastNoiseLite FNL;


    public record RenderChunk(Vector3 Position, FaceInstance[] Faces);
    public readonly ConcurrentQueue<RenderChunk> NeedRendering = [];

    /// <summary>Gets the center of a face of a cube using the standardized order: -z, +z, +y, -y, -x then +x.</summary>
    public static readonly Vector3[] FaceCenters =
    [
        new( 0.50f,  0.50f, -0.01f),
        new( 0.50f,  0.50f,  1.01f),
        new( 0.50f,  1.01f,  0.50f),
        new( 0.50f, -0.01f,  0.50f),
        new(-0.01f,  0.50f,  0.50f),
        new( 1.01f,  0.50f,  0.50f),
    ];

    public ChunkTaskGate GetChunkTaskGate(Vector3D<int> chunk, int nextStage) => (ChunkGenerationStage)nextStage switch
    {
        ChunkGenerationStage.CalculatingPoints => new ChunkTaskGate.Proceed(),
        ChunkGenerationStage.EnableInCluster => new ChunkTaskGate.Proceed(),
        ChunkGenerationStage.CullAndShade => new ChunkTaskGate.Proceed(),
        _ => new ChunkTaskGate.Halt.Complete()
    };

    public ChunkTaskType GetChunkTask(Vector3D<int> chunk, int stage) => (ChunkGenerationStage)stage switch
    {
        ChunkGenerationStage.CalculatingPoints => new ChunkTaskType.Async<Vector3D<int>>(CalculatePointsAsync),
        ChunkGenerationStage.EnableInCluster => new ChunkTaskType.Synchronous<Vector3D<int>>(EnableInCluster),
        ChunkGenerationStage.CullAndShade => new ChunkTaskType.Async<Vector3D<int>>(RenderTask),
        _ => throw new Exception($"Stage '{stage}' doesn't exist.")
    };

    public async Task CalculatePointsAsync(Vector3D<int> chunk, int stage)
    {
        Span<ushort> blocks = cluster.GetChunkByPosition(chunk);
        for (int blockZ = 0; blockZ < TChunkDims.Length; blockZ++)
        for (int blockX = 0; blockX < TChunkDims.Length; blockX++)
        for (int blockY = 0; blockY < TChunkDims.Length; blockY++)
        {
            Vector3D<int> blockPos = new Vector3D<int>(blockX, blockY, blockZ) + chunk;
            float errosion = FNL.GetNoise(blockPos.X, blockPos.Y, blockPos.Z);
            // Doesn't use Y(height) so the value is the same regardless of height.
            float mountainous = (FNL.GetNoise(blockPos.X, blockPos.Z) + 1) / 2;
            int mountainHeight = (int)(mountainous * Program.mountainHeight);
            int i = (blockZ * TChunkDims.Length + blockY) * TChunkDims.Length + blockX;
            if (blockPos.Y > mountainHeight)
                blocks[i] = Air;
            else if (blockPos.Y == mountainHeight)
                blocks[i] = Grass;
            else if (blockPos.Y > mountainHeight - 5)
                blocks[i] = Dirt;
            else
                blocks[i] = Stone;
            blocks[i] = (Math.Abs(blockPos.X) % TChunkDims.Length == 0 && blocks[i] == Grass) ? Dirt : blocks[i];
            blocks[i] = (Math.Abs(blockPos.Z) % TChunkDims.Length == 0 && blocks[i] == Grass) ? Dirt : blocks[i];
            if (errosion > 0.5f)
                blocks[i] = Air;
        }
    }

    public Task EnableInCluster(Vector3D<int> chunk, int stage)
    {
        cluster.EnableChunk(chunk);
        return Task.CompletedTask;
    }

    public async Task RenderTask(Vector3D<int> chunk, int stage)
    {
        FaceInstance[] faces = cluster.CullChunk(chunk);
        faces = ShadeBlocks(faces, chunk);
        NeedRendering.Enqueue(new((Vector3)chunk, faces));
    }

    public void CullReRender(Vector3D<int> chunk)
    {
        FaceInstance[] faces = cluster.CullChunk(chunk);
        faces = ShadeBlocks(faces, chunk);
        NeedRendering.Enqueue(new((Vector3)chunk, faces));
    }

    public FaceInstance[] ShadeBlocks(FaceInstance[] faces, Vector3D<int> chunk)
    {
        for (int f = 0; f < faces.Length; f++)
        {
            float brightness = faces[f].brightness;
            Vector3 directionVec = ((Direction)faces[f].face).ToVector();
            Vector3 facePosition = (Vector3)chunk + faces[f].position + FaceCenters[faces[f].face];
            // Sun occlusion
            float sunDot = Vector3.Dot(-sun, directionVec);
            if (sunDot > 0f)
            {
                if (cluster.Raycast(facePosition, -sun).Block != Air)
                    brightness -= sunOccludedShade;
                else
                    brightness -= sunOccludedShade * (1 - sunDot);
            }
            else
                brightness -= sunOccludedShade;
            // Sky occlusion
            RaycastResult skyRayResult = cluster.Raycast(facePosition, Vector3.UnitY);
            if (skyRayResult.Block != Air)
            {
                brightness -= skyOccludedShade * (1 - (MathF.Min(MathF.Floor(skyRayResult.Distance - 0.5f), maxSkyShadeDisWithSun) / maxSkyShadeDisWithSun));
            }
            // Ambient occlusion
            foreach (Direction testDirection in Enum.GetValues<Direction>())
            {
                Vector3 testVec = testDirection.ToVector();
                if (directionVec == testVec || directionVec == -testVec)
                    continue;
                RaycastResult testRayResult = cluster.Raycast(facePosition, testVec);
                if (testRayResult.Block != 0 && testRayResult.Distance < 1f)
                    brightness -= abientOcclusionShade;
            }
            // Cave fog
            if (facePosition.Y < 0)
                brightness *= float.Lerp(1, minBrightness, MathF.Max(facePosition.Y, -32) / -32);

            faces[f] = new(faces[f].position, faces[f].block, MathF.Max(brightness, minBrightness), faces[f].face);
        }
        return faces;
    }

    static ChunkProcessor()
    {
        FNL = new(Program.seed);
        FNL.SetFrequency(Program.worldScale);
    }
}