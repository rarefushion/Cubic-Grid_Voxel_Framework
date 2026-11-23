using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Core.Math;
using GalensUnified.CubicGrid.Framework;
using GalensUnified.CubicGrid.Framework.ChunkGeneration;
using GalensUnified.CubicGrid.Framework.Structures;
using GalensUnified.CubicGrid.Renderer.NET;
using GalensUnified.Debugging.ImGui;
using Silk.NET.Maths;

using static BlockIDs;
using static GenerationValues;

public enum ChunkGenerationStage
{
    CalculatingPoints, // Individual blocks
    EnableInCluster, // Tell the Cluster that this chunk is enabled, the cluster's tracker is not concurrent
    UpdateBehaviors
}

public class ChunkProcessor<TChunkDims>(ChunkCluster<TChunkDims> cluster): IChunkProcessor<Vector3D<int>>
where TChunkDims : IChunkDims
{
    public static int MaxStructureHeight => Tree<TChunkDims>.GetHeight;
    public static readonly int HeighestPointInChunks =
        (int)float.Ceiling((MountainHeight + MaxStructureHeight + MinTerrainHeight) / (float)TChunkDims.Length);
    public static readonly int LowestPointInChunks = HeighestPointInChunks - Program.WorldHeightInChunks;

    public readonly ConcurrentDictionary<Vector3D<int>, ConcurrentQueue<Action>> NeedsProcessingByChunk = [];
    public readonly DeltaLogs chunkGenerationTimes = new("Block Gen T1K", 1000);

    private static readonly IStructureGeneration[] structures =
    [
        new Tree<TChunkDims>(),
        new WaterSourceStructure<TChunkDims>()
    ];
    private readonly ChunkCluster<TChunkDims> cluster = cluster;

    public ChunkTaskGate GetChunkTaskGate(Vector3D<int> chunk, int nextStage) => (ChunkGenerationStage)nextStage switch
    {
        ChunkGenerationStage.CalculatingPoints => new ChunkTaskGate.Proceed(),
        ChunkGenerationStage.EnableInCluster => new ChunkTaskGate.Proceed(),
        ChunkGenerationStage.UpdateBehaviors => new ChunkTaskGate.Proceed(),
        _ => new ChunkTaskGate.Halt.Complete()
    };

    public ChunkTaskType GetChunkTask(Vector3D<int> chunk, int stage) => (ChunkGenerationStage)stage switch
    {
        ChunkGenerationStage.CalculatingPoints => new ChunkTaskType.Async<Vector3D<int>>(CalculatePointsAsync),
        ChunkGenerationStage.EnableInCluster => new ChunkTaskType.Synchronous<Vector3D<int>>(EnableInCluster),
        ChunkGenerationStage.UpdateBehaviors => new ChunkTaskType.Async<Vector3D<int>>(UpdateBlockBehaviorsTask),
        _ => throw new Exception($"Stage '{stage}' doesn't exist.")
    };

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
        Stopwatch stopwatch = Stopwatch.StartNew();
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
            Vector3D<int> localPos = new(blockX, blockY, blockZ);
            Vector3D<int> blockPos = localPos + chunk;
            int mountainHeight = GetMountainHeight(blockPos);
            int i = ChunkMath<TChunkDims>.IndexByLocalPos(localPos);
            if (blockPos.Y > mountainHeight)
                blocks[i] = DefaultAtmosphereBlock;
            else if (blockPos.Y == mountainHeight)
                blocks[i] = DefaultSurfaceBlock;
            else if (blockPos.Y >= mountainHeight - RegolithDepth)
                blocks[i] = DefaultRegolithBlock;
            else
                blocks[i] = DefaultUndergroundBlock;
            blocks[i] = (Math.Abs(blockPos.X) % TChunkDims.Length == 0 && blocks[i] == Grass) ? Dirt : blocks[i];
            blocks[i] = (Math.Abs(blockPos.Z) % TChunkDims.Length == 0 && blocks[i] == Grass) ? Dirt : blocks[i];
            if (blockPos.Y <= mountainHeight && IsErodid(blockPos))
                blocks[i] = DefaultCaveVoidBlock;
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
        stopwatch.Stop();
        chunkGenerationTimes.LogDelta(stopwatch.Elapsed.Milliseconds);
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
            Vector3D<int> localPos = new(blockX, blockY, blockZ);
            Vector3D<int> blockPos = localPos + chunk;
            int i = ChunkMath<TChunkDims>.IndexByLocalPos(localPos);
            ChunkCluster<TChunkDims>.IBlockData? blockData = CreateBlockData(blockPos, blocks[i]);
            if (blockData != null)
            {
                if (!cluster.TryGetBlockData<ChunkCluster<TChunkDims>.IBlockData>(blockPos, out _))
                    cluster.TrySetBlockData(blockPos, blockData);
                if (blockData is WaterBlockData<TChunkDims> waterData)
                    cluster.TryUpdateBlockData(blockPos,  new ChunkCluster<TChunkDims>.BlockUpdate<string>("Spawned"));
            }
        }
    }

    public static ChunkCluster<TChunkDims>.IBlockData? CreateBlockData(Vector3D<int> blockPosition, ushort block)
    {
        if (block == Grass || block == OakLeaves)
            return new TintBlockData<TChunkDims>(GetBiomeTint((Vector3)blockPosition));
        if (WaterRendering.IsWater(block))
            return new WaterBlockData<TChunkDims>(WaterLevels, GetWaterTint((Vector3)blockPosition));
        return null;
    }
}