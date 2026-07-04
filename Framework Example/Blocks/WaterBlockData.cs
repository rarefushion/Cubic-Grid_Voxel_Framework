using System.Collections.Concurrent;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Framework;
using Silk.NET.Maths;

using static BlockIDs;

public class WaterBlockData<TChunkDims>() :
ChunkCluster<TChunkDims>.IBlockBehavior,
ChunkCluster<TChunkDims>.IBlockData
where TChunkDims : IChunkDims
{
    public int level = WaterLevels;

    public static ChunkCluster<TChunkDims> cluster;
    public static ChunkProcessor<TChunkDims> processor;

    public void Update<TPayload>
    (
        Vector3D<int> blockPosition,
        ChunkCluster<TChunkDims>.IBlockData blockData,
        ChunkCluster<TChunkDims>.BlockUpdate<TPayload> blockUpdate
    )
    {
        WaterBlockData<TChunkDims> data = (WaterBlockData<TChunkDims>)blockData;
        Vector3D<int> rootChunk = blockPosition.FloorTo(TChunkDims.Length);
        if (TrySpread(blockPosition + -Vector3D<int>.UnitY, rootChunk, data, Direction.Bottom))
            return;
        if (data.level <= 0)
            return;
        for (Direction d = 0; d < (Direction)6; d++)
            if (d != Direction.Top && d != Direction.Bottom)
                TrySpread(blockPosition + d.ToVector().Floor(), rootChunk, data, d);
    }

    private bool TrySpread(Vector3D<int> testPosition, Vector3D<int> rootChunk, WaterBlockData<TChunkDims> blockData, Direction to)
    {
        if (!cluster.TryGetBlock(testPosition, out ushort? testBlock))
            return false;

        int nextLevel = blockData.level - 1;
        if (to == Direction.Bottom)
            nextLevel = WaterLevels;
        if (nextLevel == 0)
            return false;
        if (WaterRendering.IsWater(testBlock!.Value))
        {
            if (WaterRendering.GetLevel(testBlock!.Value) >= nextLevel)
                return true;
        }
        else if (testBlock != Air)
            return false;

        Vector3D<int> testChunk = testPosition.FloorTo(TChunkDims.Length);
        if (testChunk == rootChunk)
            Spread(testPosition, nextLevel, to);
        else
        {
            ConcurrentQueue<Action> queue = processor.NeedsProcessingByChunk.GetOrAdd(testChunk, _ => []);
            queue.Enqueue(() => Spread(testPosition, nextLevel, to));
        }
        return true;
    }

    private void Spread(Vector3D<int> testPosition, int level, Direction to)
    {
        if (!cluster.TryGetBlock(testPosition, out ushort? testBlock))
            return;
        if (testBlock != Air && !WaterRendering.IsWater(testBlock!.Value))
            return;
        WaterBlockData<TChunkDims> data = new() { level = level };
        if (!cluster.TrySetBlock(testPosition, WaterRendering.GetBlock(data.level)))
            return;

        cluster.TrySetBlockData(testPosition, data);
        cluster.TryUpdateBlockData(testPosition, new ChunkCluster<TChunkDims>.BlockUpdate<string>("Water Update"));
    }
}