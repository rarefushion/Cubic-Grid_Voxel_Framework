using System.Numerics;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Core.Math;
using Silk.NET.Maths;
using static GalensUnified.CubicGrid.Core.Raycasting;

namespace GalensUnified.CubicGrid.Framework.Player;

/// <summary>Basic player interactions.</summary>
public class Interactions
{
    public static RaycastResult AttemptBreak<TDims>(ChunkCluster<TDims> cluster, Vector3 position, Vector3 direction, float range, Action<Vector3D<int>> chunkUpdate)
    where TDims : IChunkDims
    {
        range = float.Min(range, 999999);
        RaycastResult result = cluster.Raycast(position, direction, range);
        if (result.Block != 0 && result.Distance <= range)
            SetBlock(cluster, result.BlockPosition, 0, null, chunkUpdate);
        return result;
    }

    public static void SetBlock<TDims>
    (
        ChunkCluster<TDims> cluster,
        Vector3D<int> blockPos,
        ushort setTo,
        ChunkCluster<TDims>.IBlockData? blockData,
        Action<Vector3D<int>> chunkUpdate
    )
    where TDims : IChunkDims
    {
        Vector3D<int> chunkPos = blockPos.FloorTo(TDims.Length);
        if (!cluster.TrySetBlock(blockPos, setTo))
            return;
        if (blockData != null)
            cluster.TrySetBlockData(blockPos, blockData);
        else
            cluster.TryRemoveBlockData(blockPos);
        for (Direction d = 0; d < (Direction)6; d++)
        {
            Vector3D<int> testPosition = blockPos + d.ToVector().Floor();
            cluster.TryUpdateBlockData(testPosition, new ChunkCluster<TDims>.BlockUpdate<string>("Neighbor Broken"));
            if (!ChunkMath<TDims>.PosLocal(ChunkMath<TDims>.LocalPosByGlobalPos(blockPos) + d.ToVector().Floor()))
                chunkUpdate(chunkPos + d.ToVector().Floor() * TDims.Length);
        }
        chunkUpdate(chunkPos);
    }
}