using System.Numerics;
using GalensUnified.CubicGrid.Core;
using Silk.NET.Maths;
using static GalensUnified.CubicGrid.Core.Raycasting;

namespace GalensUnified.CubicGrid.Framework.Player;

/// <summary>Basic player interactions.</summary>
public class Interactions
{
    public static bool AttemptBreak<TDims>
    (
        ChunkCluster<TDims> cluster,
        Vector3 position,
        Vector3 direction,
        float range,
        out Vector3D<int>? modifiedPosition, out RaycastResult raycastResult
    )
    where TDims : IChunkDims
    {
        range = float.Min(range, 999999);
        raycastResult = cluster.Raycast(position, direction, range);
        if
        (
            raycastResult.Block != 0 &&
            raycastResult.Distance <= range &&
            SetBlock(cluster, raycastResult.BlockPosition, 0, null)
        )
        {
            modifiedPosition = raycastResult.BlockPosition;
            return true;
        }
        modifiedPosition = null;
        return false;
    }

    public static bool AttemptPlace<TDims>
    (
        ChunkCluster<TDims> cluster,
        Vector3 position,
        Vector3 direction,
        float range,
        ushort setTo,
        ChunkCluster<TDims>.IBlockData? blockData,
        out Vector3D<int>? modifiedPosition, out RaycastResult raycastResult
    )
    where TDims : IChunkDims
    {
        range = float.Min(range, 999999);
        raycastResult = cluster.Raycast(position, direction, range);
        Vector3D<int> previousBlockPosition = raycastResult.BlockPosition + raycastResult.Normal;
        if
        (
            cluster.TryGetBlock(previousBlockPosition, out ushort? previousBlock) &&
            previousBlock == 0 &&
            raycastResult.Distance <= range &&
            SetBlock(cluster, previousBlockPosition, setTo, blockData)
        )
        {
            modifiedPosition = previousBlockPosition;
            return true;
        }
        modifiedPosition = null;
        return false;
    }

    public static bool SetBlock<TDims>
    (
        ChunkCluster<TDims> cluster,
        Vector3D<int> blockPos,
        ushort setTo,
        ChunkCluster<TDims>.IBlockData? blockData
    )
    where TDims : IChunkDims
    {
        if (!cluster.TrySetBlock(blockPos, setTo))
            return false;
        if (blockData != null)
            cluster.TrySetBlockData(blockPos, blockData);
        else
            cluster.TryRemoveBlockData(blockPos);
        return true;
    }
}