using System.Numerics;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Core.Math;
using Silk.NET.Maths;
using static GalensUnified.CubicGrid.Core.Raycasting;

namespace GalensUnified.CubicGrid.Framework.Player;

/// <summary>Basic player interactions.</summary>
public class Interactions
{
    // Will break out into SetBlock later.
    public static RaycastResult AttemptBreak<TDims>(ChunkCluster<TDims> cluster, Vector3 position, Vector3 direction, float range, Action<Vector3D<int>> chunkUpdate)
    where TDims : IChunkDims
    {
        RaycastResult result = cluster.Raycast(position, direction);
        if (result.Block != 0 && result.Distance <= range)
        {
            Vector3D<int> chunkPos = result.BlockPosition.FloorTo(TDims.Length);
            if (!cluster.TrySetBlock(result.BlockPosition, 0))
                return result;
            chunkUpdate(chunkPos);
            for (Direction d = 0; d < (Direction)6; d++)
                if (!ChunkMath<TDims>.PosLocal(ChunkMath<TDims>.LocalPosByGlobalPos(result.BlockPosition) + d.ToVector().Floor()))
                    chunkUpdate(chunkPos + d.ToVector().Floor() * TDims.Length);
        }
        return result;
    }
}