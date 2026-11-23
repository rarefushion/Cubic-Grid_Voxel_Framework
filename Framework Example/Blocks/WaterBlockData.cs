using System.Collections.Concurrent;
using System.Numerics;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Framework;
using Silk.NET.Maths;

using static BlockIDs;

public class WaterBlockData<TChunkDims>(int level, Vector3 tint) :
ChunkCluster<TChunkDims>.IBlockBehavior,
ChunkCluster<TChunkDims>.IBlockData
where TChunkDims : IChunkDims
{
    public int level = level;
    public Vector3 tint = tint;

    public static ChunkCluster<TChunkDims> cluster;
    public static ChunkProcessor<TChunkDims> processor;

    public void Update<TPayload>
    (
        Vector3D<int> blockPosition,
        ChunkCluster<TChunkDims>.IBlockData blockData,
        ChunkCluster<TChunkDims>.BlockUpdate<TPayload> blockUpdate
    )
    {
        string payload = string.Empty;
        if (blockUpdate is ChunkCluster<TChunkDims>.BlockUpdate<string> stringUpdate)
            payload = stringUpdate.Payload;
        WaterBlockData<TChunkDims> data = (WaterBlockData<TChunkDims>)blockData;
        Vector3D<int> rootChunk = blockPosition.FloorTo(TChunkDims.Length);
        if (TrySpread(blockPosition + -Vector3D<int>.UnitY, rootChunk, data, Direction.Bottom, payload))
            return;
        if (data.level <= 0)
            return;
        for (Direction d = 0; d < (Direction)6; d++)
            if (d != Direction.Top && d != Direction.Bottom)
                TrySpread(blockPosition + d.ToVector().Floor(), rootChunk, data, d, payload);
    }

    private bool TrySpread(Vector3D<int> testPosition, Vector3D<int> rootChunk, WaterBlockData<TChunkDims> blockData, Direction to, string payload)
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
        if (testChunk == rootChunk || payload == "Neighbor Updated")
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
        WaterBlockData<TChunkDims> data = new(level, GenerationValues.GetWaterTint((Vector3)testPosition));
        if (!cluster.TrySetBlock(testPosition, WaterRendering.GetBlock(data.level)))
            return;

        cluster.TrySetBlockData(testPosition, data);
        cluster.TryUpdateBlockData(testPosition, new ChunkCluster<TChunkDims>.BlockUpdate<string>("Water Update"));
    }
}