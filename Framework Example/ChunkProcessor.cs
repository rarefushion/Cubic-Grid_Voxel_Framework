using System.Collections.Concurrent;
using System.Numerics;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Framework;
using GalensUnified.CubicGrid.Framework.Structures;
using GalensUnified.CubicGrid.Renderer.NET;
using Silk.NET.Maths;

using static BlockIDs;
using static GenerationValues;

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
    public static int MaxStructureHeight => Tree<TChunkDims>.GetHeight;
    public static readonly int HeighestPointInChunks =
        (int)float.Ceiling((MountainHeight + MaxStructureHeight + MinTerrainHeight) / (float)TChunkDims.Length);
    public static readonly int LowestPointInChunks = HeighestPointInChunks - Program.WorldHeightInChunks;

    public record RenderChunk(Vector3 Position, ShapeInstance[] Shapes);
    public readonly ConcurrentQueue<RenderChunk> NeedRendering = [];
    public readonly ConcurrentDictionary<Vector3D<int>, ConcurrentQueue<Action>> NeedsProcessingByChunk = [];

    private static readonly IStructureGeneration[] structures =
    [
        new Tree<TChunkDims>(),
        new WaterSourceStructure<TChunkDims>()
    ];
    private readonly ChunkCluster<TChunkDims> cluster = cluster;
    private readonly Shader shader = shader;

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
            if (WaterRendering.IsWater(blocks[i]))
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
        ShapeInstancer<TChunkDims> cullingHandler = new((Vector3)chunk, sunDirection, sunOccludedShade, minBrightness, cluster);
        cullingHandler = cluster.CullChunk(chunk, cullingHandler);
        shader.DeactivateChunk((Vector3)chunk);
        if (cluster.IsActive(chunk) && cullingHandler.instances.Count > 0)
            shader.RenderChunk((Vector3)chunk, [.. cullingHandler.instances]);
    }

    private void CullAndShadeChunk(Vector3D<int> chunk)
    {
        ShapeInstancer<TChunkDims> cullingHandler = new((Vector3)chunk, sunDirection, sunOccludedShade, minBrightness, cluster);
        cullingHandler = cluster.CullChunk(chunk, cullingHandler);
        NeedRendering.Enqueue(new((Vector3)chunk, [.. cullingHandler.instances]));
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
}