using System.Numerics;
using Silk.NET.Maths;
using static GalensUnified.CubicGrid.Core.Math.RegionMath;

namespace GalensUnified.CubicGrid.Framework;

public partial class ChunkCluster
{
    /// <summary>The result of a raycast. Always garenteed to have a Block value. 0 if missed.</summary>
    public abstract record RaycastResult(ushort Block);
    public record RaycastHit(ushort Block, Vector3D<int> BlockPosition, float Distance) : RaycastResult(Block);
    public record RaycastMissed(ushort Block) : RaycastResult(Block);
    public static readonly RaycastMissed RaycastMiss = new(0);

    /// <summary>DDA Raycast over the entire cluster until the ray hits a block or leaves <see cref="activeChunks"/>.</summary>
    /// <param name="pos">Starting position of the ray.</param>
    /// <param name="dir">Direction the ray will travel.</param>
    /// <returns>
    /// If ray leaves <see cref="activeChunks"/><br/>
    /// ⠀⠀⠀⠀<see cref="RaycastMissed"/> where Block is 0.<br/>
    /// If ray hits block<br/>
    /// ⠀⠀⠀⠀<see cref="RaycastHit"/>
    /// with the hit block, it's global position and distance form ray's starting position.
    /// </returns>
    public RaycastResult Raycast(Vector3 pos, Vector3 dir)
    {
        dir = Vector3.Normalize(dir);
        Vector3D<int> blockPos = pos.Floor();
        int stepX = dir.X < 0 ? -1 : 1;
        int stepY = dir.Y < 0 ? -1 : 1;
        int stepZ = dir.Z < 0 ? -1 : 1;
        int stepStrideX = stepX * 1;
        int stepStrideY = stepY * chunkLength;
        int stepStrideZ = stepZ * chunkLength * chunkLength;
        float deltaDistX = MathF.Abs(1f / dir.X);
        float deltaDistY = MathF.Abs(1f / dir.Y);
        float deltaDistZ = MathF.Abs(1f / dir.Z);
        float sideDistX = dir.X < 0 ? (pos.X - blockPos.X) * deltaDistX : (blockPos.X + 1f - pos.X) * deltaDistX;
        float sideDistY = dir.Y < 0 ? (pos.Y - blockPos.Y) * deltaDistY : (blockPos.Y + 1f - pos.Y) * deltaDistY;
        float sideDistZ = dir.Z < 0 ? (pos.Z - blockPos.Z) * deltaDistZ : (blockPos.Z + 1f - pos.Z) * deltaDistZ;
        Vector3D<int> chunkPos = new
            (
                (int)MathF.Floor((float)blockPos.X / chunkLength) * chunkLength,
                (int)MathF.Floor((float)blockPos.Y / chunkLength) * chunkLength,
                (int)MathF.Floor((float)blockPos.Z / chunkLength) * chunkLength
            );
        while (activeChunks.Contains(chunkPos))
        {
            int chunkIndex = IndexByChunkCoord(ChunkCoordByGlobalPos(chunkPos));
            int blockIndex = IndexByGlobalPos(blockPos, chunkLength);
            while(PosLocal(blockPos - chunkPos, chunkLength))
            {
                if (flattenedChunks[blockIndex + chunkIndex] != 0)
                    return new RaycastHit(flattenedChunks[blockIndex + chunkIndex], blockPos, ((Vector3)blockPos - pos).Length());
                // Step along the shortest sideDist
                if (sideDistX < sideDistY && sideDistX < sideDistZ)
                {
                    sideDistX += deltaDistX;
                    blockPos.X += stepX;
                    blockIndex += stepStrideX;
                }
                else if (sideDistY < sideDistZ)
                {
                    sideDistY += deltaDistY;
                    blockPos.Y += stepY;
                    blockIndex += stepStrideY;
                }
                else
                {
                    sideDistZ += deltaDistZ;
                    blockPos.Z += stepZ;
                    blockIndex += stepStrideZ;
                }
            }
            chunkPos = new
            (
                (int)MathF.Floor((float)blockPos.X / chunkLength) * chunkLength,
                (int)MathF.Floor((float)blockPos.Y / chunkLength) * chunkLength,
                (int)MathF.Floor((float)blockPos.Z / chunkLength) * chunkLength
            );
        }
        return RaycastMiss;
    }
}