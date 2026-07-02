using System.Collections.Concurrent;
using System.Numerics;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Core.Math;
using GalensUnified.CubicGrid.Framework;
using GalensUnified.CubicGrid.Framework.Structures;
using GalensUnified.CubicGrid.Renderer.NET;
using Silk.NET.Maths;

using static BlockIDs;
using static GalensUnified.CubicGrid.Core.Raycasting;

public enum ChunkGenerationStage
{
    CalculatingPoints, // Individual blocks
    EnableInCluster, // Tell the Cluster that this chunk is enabled, the cluster's tracker is not concurrent
    UpdateBehaviors,
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
    public const int MinTerrainHeight = 0;
    public static readonly int HeighestPointInChunks =
        (int)float.Ceiling((Program.mountainHeight + MaxStructureHeight + MinTerrainHeight) / (float)TChunkDims.Length);
    public static readonly int LowestPointInChunks = HeighestPointInChunks - Program.WorldHeightInChunks;

    public static int MaxStructureHeight => Tree<TChunkDims>.GetHeight;
    private readonly Vector3 sun = sunDirection;

    private readonly ChunkCluster<TChunkDims> cluster = cluster;
    private readonly Shader shader = shader;
    private static readonly FastNoiseLite FNL;
    private static readonly IStructureGeneration[] structures =
    [
        new Tree<TChunkDims>(),
        new WaterSourceStructure<TChunkDims>()
    ];


    public record RenderChunk(Vector3 Position, CubeFaceInstance[] Faces);
    public readonly ConcurrentQueue<RenderChunk> NeedRendering = [];
    public readonly ConcurrentDictionary<Vector3D<int>, ConcurrentQueue<Action>> NeedsProcessingByChunk = [];

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
        ChunkGenerationStage.UpdateBehaviors => new ChunkTaskGate.Proceed(),
        ChunkGenerationStage.CullAndShade => new ChunkTaskGate.Proceed(),
        _ => new ChunkTaskGate.Halt.Complete()
    };

    public ChunkTaskType GetChunkTask(Vector3D<int> chunk, int stage) => (ChunkGenerationStage)stage switch
    {
        ChunkGenerationStage.CalculatingPoints => new ChunkTaskType.Async<Vector3D<int>>(CalculatePointsAsync),
        ChunkGenerationStage.EnableInCluster => new ChunkTaskType.Synchronous<Vector3D<int>>(EnableInCluster),
        ChunkGenerationStage.UpdateBehaviors => new ChunkTaskType.Async<Vector3D<int>>(UpdateBlockBehaviorsTask),
        ChunkGenerationStage.CullAndShade => new ChunkTaskType.Async<Vector3D<int>>(RenderTask),
        _ => throw new Exception($"Stage '{stage}' doesn't exist.")
    };

    public static bool IsErodid(Vector3D<int> blockPosition)
    {
        float errosion = FNL.GetNoise(blockPosition.X, blockPosition.Y, blockPosition.Z);
        return errosion > 0.5f;
    }

    public static int GetMountainHeight(Vector3D<int> blockPosition)
    {
        // Doesn't use Y(height) so the value is the same regardless of height.
        float mountainous = (FNL.GetNoise(blockPosition.X, blockPosition.Z) + 1) / 2;
        return (int)(mountainous * Program.mountainHeight) + MinTerrainHeight;
    }

    private async Task CalculatePointsAsync(Vector3D<int> chunk, int stage)
    {
        if (!Program.lockGenerationHeight)
            CalculateChunkPointsAsync(chunk);
        else
        {
            for (int y = LowestPointInChunks; y < HeighestPointInChunks; y++)
            {
                chunk.Y = y * TChunkDims.Length;
                CalculateChunkPointsAsync(chunk);
            }
        }
    }

    public void CalculateChunkPointsAsync(Vector3D<int> chunk)
    {
        // Find Structures
        Dictionary<IStructureGeneration, GeneratedStructureData[]> structureDataByType = [];
        foreach (IStructureGeneration structure in structures)
        {
            List<GeneratedStructureData> dataPoints = [];
            foreach (Vector3D<int> checkChunk in structure.PossibleChunks(chunk))
                dataPoints.AddRange(structure.FindChunksStructures(checkChunk));
            structureDataByType.Add(structure, [.. dataPoints]);
        }
        // Blocks
        Span<ushort> blocks = cluster.GetChunkByPosition(chunk);
        for (int blockZ = 0; blockZ < TChunkDims.Length; blockZ++)
        for (int blockX = 0; blockX < TChunkDims.Length; blockX++)
        for (int blockY = 0; blockY < TChunkDims.Length; blockY++)
        {
            Vector3D<int> blockPos = new Vector3D<int>(blockX, blockY, blockZ) + chunk;
            int mountainHeight = GetMountainHeight(blockPos);
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
            if (IsErodid(blockPos))
                blocks[i] = Air;
            // Place Structures
            foreach ((IStructureGeneration type, GeneratedStructureData[] dataEntries) in structureDataByType)
            foreach (GeneratedStructureData data in dataEntries)
            {
                Vector3D<int> blockPosLocalToStructure = data.LocalPositionByGlobalPos(blockPos);
                ushort structureBlock = type.GetBlock(blockPosLocalToStructure);
                if (structureBlock != 0)
                    blocks[i] = structureBlock;
            }
        }
    }

    public Task EnableInCluster(Vector3D<int> chunk, int stage)
    {
        if (!Program.lockGenerationHeight)
            cluster.EnableChunk(chunk);
        else
            for (int y = LowestPointInChunks; y < HeighestPointInChunks; y++)
            {
                chunk.Y = y * TChunkDims.Length;
                cluster.EnableChunk(chunk);
            }
        return Task.CompletedTask;
    }

    public async Task UpdateBlockBehaviorsTask(Vector3D<int> chunk, int stage)
    {
        if (!Program.lockGenerationHeight)
            UpdateBlockBehaviors(chunk);
        else
            for (int y = LowestPointInChunks; y < HeighestPointInChunks; y++)
            {
                chunk.Y = y * TChunkDims.Length;
                UpdateBlockBehaviors(chunk);
            }
    }

    public void UpdateBlockBehaviors(Vector3D<int> chunk)
    {
        Span<ushort> blocks = cluster.GetChunkByPosition(chunk);
        for (int blockZ = 0; blockZ < TChunkDims.Length; blockZ++)
        for (int blockX = 0; blockX < TChunkDims.Length; blockX++)
        for (int blockY = 0; blockY < TChunkDims.Length; blockY++)
        {
            int i = (blockZ * TChunkDims.Length + blockY) * TChunkDims.Length + blockX;
            if (blocks[i] == Water)
            {
                Vector3D<int> blockPos = new Vector3D<int>(blockX, blockY, blockZ) + chunk;
                if (!cluster.TryGetBlockData<WaterBlockData<TChunkDims>>(blockPos, out _))
                    cluster.TrySetBlockData(blockPos, new WaterBlockData<TChunkDims>());
                cluster.TryUpdateBlockData(blockPos,  new ChunkCluster<TChunkDims>.BlockUpdate<string>("Spawned"));
            }
        }
    }

    public async Task RenderTask(Vector3D<int> chunk, int stage)
    {
        if (!Program.lockGenerationHeight)
            CullAndShadeChunk(chunk);
        else
            for (int y = LowestPointInChunks; y < HeighestPointInChunks; y++)
            {
                chunk.Y = y * TChunkDims.Length;
                CullAndShadeChunk(chunk);
            }
    }

    public void CullReRender(Vector3D<int> chunk)
    {
        if (!Program.lockGenerationHeight)
        {
            UpdateBlockBehaviors(chunk);
            Program.backgroundThreadBatch.EnqueueJob(() => CullAndShadeChunk(chunk));
        }
        else
            for (int y = LowestPointInChunks; y < HeighestPointInChunks; y++)
            {
                Vector3D<int> subChunk = chunk;
                subChunk.Y = y * TChunkDims.Length;
                UpdateBlockBehaviors(subChunk);
                Program.backgroundThreadBatch.EnqueueJob(() => CullAndShadeChunk(subChunk));
            }
    }

    public void Redraw(Vector3D<int> chunk) =>
        Program.backgroundThreadBatch.EnqueueJob(() => CullAndShadeChunk(chunk));

    public void RedrawInstant(Vector3D<int> chunk)
    {
        CullingHandler cullingHandler = new((Vector3)chunk, sunDirection, sunOccludedShade, minBrightness, cluster);
        cullingHandler = cluster.CullChunk(chunk, cullingHandler);
        shader.DeactivateChunk((Vector3)chunk);
        if (cluster.IsActive(chunk) && cullingHandler.instances.Count > 0)
            shader.RenderChunk((Vector3)chunk, [.. cullingHandler.instances]);
    }

    private void CullAndShadeChunk(Vector3D<int> chunk)
    {
        CullingHandler cullingHandler = new((Vector3)chunk, sunDirection, sunOccludedShade, minBrightness, cluster);
        cullingHandler = cluster.CullChunk(chunk, cullingHandler);
        NeedRendering.Enqueue(new((Vector3)chunk, [.. cullingHandler.instances]));
    }

    struct CullingHandler
    (
        Vector3 chunkPosition,
        Vector3 sunDirection,
        float sunOccludedShade,
        float minBrightness,
        ChunkCluster<TChunkDims> cluster
    ) : IBlockCullingHandler
    {
        public readonly List<CubeFaceInstance> instances = [];

        public static FastNoiseLite temperature = new(Program.seed);
        static readonly Vector3 lush = new(0.0f, 1.0f, 0.0f); // rgb(0, 255, 0)
        static readonly Vector3 autumn = new(1, 0.53f, 0.17f); // rgb(255, 136, 44)
        static readonly Vector3 autumnWater = new(0.39f, 0.0f, 1.0f); // rgb(100, 0, 255)
        static readonly Vector3 lushWater = new(0.0f, 1.0f, 1.0f); // rgb(0, 255, 255)

        public static float GetTemperature(Vector3 position) =>
            (temperature.GetNoise(position.X, position.Z) + 1) / 2;

        public readonly void CullBegan() { }

        public readonly void FaceVisible(Vector3 localBlockPosition, ushort block, Direction faceNormal)
        {
            Vector3 blockPos = chunkPosition + localBlockPosition;
            // Shade
            float brightness = 1;
            Vector3 directionVec = faceNormal.ToVector();
            Vector3 facePosition = blockPos + FaceCenters[(int)faceNormal];
            // Sun occlusion
            float sunDot = Vector3.Dot(-sunDirection, directionVec);
            if (sunDot > 0f)
            {
                if (cluster.Raycast(facePosition, -sunDirection).Block != Air)
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
            if (facePosition.Y < MinTerrainHeight)
                brightness *= float.Lerp(1, minBrightness, (MathF.Max(facePosition.Y, MinTerrainHeight - 32) - MinTerrainHeight) / -32);
            brightness = MathF.Max(brightness, minBrightness);
            // Tint
            Vector3 tint = Vector3.One;
            if (block == Grass && faceNormal != Direction.Bottom) // Only Grass, not bottoms
            {
                tint = Vector3.Lerp(autumn, lush, GetTemperature(blockPos));
                if (faceNormal != Direction.Top) // All sides
                {
                    // Create another face for the Grass Side to fill in the bottom with dirt with no tint.
                    instances.Add(new(localBlockPosition, GrassSideDirt, Vector3.One * brightness, (int)faceNormal));
                }
            }
            if (block == OakLeaves)
                tint = Vector3.Lerp(autumn, lush, GetTemperature(blockPos));
            if (block == Water)
                tint = Vector3.Lerp(autumnWater, lushWater, GetTemperature(blockPos));

            instances.Add(new(localBlockPosition, block, tint * brightness, (int)faceNormal));
        }
    }

    public void Deactivate(Vector3D<int> chunk)
    {
        if (!Program.lockGenerationHeight)
        {
            shader.DeactivateChunk((Vector3)chunk);
            cluster.TryRemoveChunk(chunk);
        }
        else
            for (int y = LowestPointInChunks; y < HeighestPointInChunks; y++)
            {
                chunk.Y = y * TChunkDims.Length;
                shader.DeactivateChunk((Vector3)chunk);
                cluster.TryRemoveChunk(chunk);
            }
    }

    static ChunkProcessor()
    {
        FNL = new(Program.seed);
        FNL.SetFrequency(Program.worldScale);
        CullingHandler.temperature.SetFrequency(Program.worldScale * 0.2f);
    }
}