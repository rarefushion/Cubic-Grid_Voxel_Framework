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
    public int level = 6;

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
        if (TrySpread(blockPosition + -Vector3D<int>.UnitY, rootChunk, data, true))
            return;
        if (data.level <= 0)
            return;
        for (Direction d = 0; d < (Direction)6; d++)
            if (d != Direction.Top && d != Direction.Bottom)
                TrySpread(blockPosition + d.ToVector().Floor(), rootChunk, data, false);
    }

    private bool TrySpread(Vector3D<int> testPosition, Vector3D<int> rootChunk, WaterBlockData<TChunkDims> blockData, bool fall)
    {
        if (!cluster.TryGetBlock(testPosition, out ushort? testBlock))
            return false;
        if (testBlock == Water)
            return true;
        if (testBlock != Air)
            return false;

        Vector3D<int> testChunk = testPosition.FloorTo(TChunkDims.Length);
        if (testChunk == rootChunk)
            Spread(testPosition, blockData.level, fall);
        else
        {
            ConcurrentQueue<Action> queue = processor.NeedsProcessingByChunk.GetOrAdd(testChunk, _ => []);
            queue.Enqueue(() => Spread(testPosition, blockData.level, fall));
        }
        return true;
    }

    private void Spread(Vector3D<int> testPosition, int lastLevel, bool fall)
    {
        if (!cluster.TryGetBlock(testPosition, out ushort? testBlock) || testBlock != Air)
            return;
        if (!cluster.TrySetBlock(testPosition, Water))
            return;

        WaterBlockData<TChunkDims> data = new();
        if (!fall)
            data.level = lastLevel -  1;
        cluster.TrySetBlockData(testPosition, data);
        cluster.TryUpdateBlockData(testPosition, new ChunkCluster<TChunkDims>.BlockUpdate<string>("Water Update"));
    }
}