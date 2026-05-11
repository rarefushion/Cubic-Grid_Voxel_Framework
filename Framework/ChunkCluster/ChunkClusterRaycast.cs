using System.Numerics;
using Silk.NET.Maths;
using GalensUnified.CubicGrid.Core;
using static GalensUnified.CubicGrid.Core.Math.RegionMath;

namespace GalensUnified.CubicGrid.Framework;

public partial class ChunkCluster
{
    /// <summary>The result of a raycast. Always garenteed to have a Block value. 0 if missed.</summary>
    public abstract record RaycastResult(ushort Block);
    /// <summary>Returned when a Raycast hits a block.</summary>
    /// <param name="Block">The block that was hit.</param>
    /// <param name="BlockPosition">The blocks global position.</param>
    /// <param name="Normal">The direction the block was hit from.</param>
    /// <param name="Distance">The distance the ray travelled before it hit a block.</param>
    /// <remarks>
    /// Extra values can be determined like so:<br/>
    /// The previous block can be calculated by using <paramref name="BlockPosition"/> + <paramref name="Normal"/>.<br/>
    /// The precise hit point can be calculated by remembering the ray start position and direction, direction * <paramref name="Distance"/> + start.
    /// </remarks>
    public record RaycastHit(ushort Block, Vector3D<int> BlockPosition, Vector3D<int> Normal, float Distance) : RaycastResult(Block);
    public record RaycastMissed() : RaycastResult(0);
    public static readonly RaycastMissed RaycastMiss = new();

    /// <summary>DDA Raycast over the entire cluster until the ray hits a block or leaves <see cref="activeChunks"/>.</summary>
    /// <param name="pos">Starting position of the ray.</param>
    /// <param name="dir">Direction the ray will travel.</param>
    /// <returns>
    /// If ray leaves <see cref="activeChunks"/><br/>
    /// ⠀⠀⠀⠀<see cref="RaycastMissed"/> where Block is 0.<br/>
    /// If ray hits block<br/>
    /// ⠀⠀⠀⠀<see cref="RaycastHit"/>
    /// with the hit block and extra information.
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
        bool stepPrevX = false;
        bool stepPrevY = false;
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
        while (IsActive(chunkPos))
        {
            int chunkIndex = IndexByChunkCoord(ChunkCoordByGlobalPos(chunkPos));
            int blockIndex = IndexByGlobalPos(blockPos, chunkLength);
            while(PosLocal(blockPos - chunkPos, chunkLength))
            {
                if (flattenedChunks[blockIndex + chunkIndex] != 0)
                {
                    Vector3D<int> normal;
                    float distance;
                    if (blockPos == pos.Floor())
                    {
                        distance = 0;
                        normal = new(0, 0, 0);
                    }
                    else if (stepPrevX)
                    {
                        distance = sideDistX - deltaDistX;
                        normal = new(stepX, 0, 0);
                    }
                    else if (stepPrevY)
                    {
                        distance = sideDistY - deltaDistY;
                        normal = new(0, stepY, 0);
                    }
                    else
                    {
                        distance = sideDistZ - deltaDistZ;
                        normal = new(0, 0, stepZ);
                    }
                    return new RaycastHit(flattenedChunks[blockIndex + chunkIndex], blockPos, normal, distance);
                }
                // Step along the shortest sideDist
                if (sideDistX < sideDistY && sideDistX < sideDistZ)
                {
                    sideDistX += deltaDistX;
                    blockPos.X += stepX;
                    blockIndex += stepStrideX;
                    stepPrevX = true;
                    stepPrevY = false;
                }
                else if (sideDistY < sideDistZ)
                {
                    sideDistY += deltaDistY;
                    blockPos.Y += stepY;
                    blockIndex += stepStrideY;
                    stepPrevX = false;
                    stepPrevY = true;
                }
                else
                {
                    sideDistZ += deltaDistZ;
                    blockPos.Z += stepZ;
                    blockIndex += stepStrideZ;
                    stepPrevX = false;
                    stepPrevY = false;
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