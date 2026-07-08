using System.Numerics;
using Silk.NET.Maths;
using GalensUnified.CubicGrid.Core;
using static GalensUnified.CubicGrid.Core.Raycasting;
using GalensUnified.CubicGrid.Core.Math;

namespace GalensUnified.CubicGrid.Framework;

public partial class ChunkCluster<TChunkDims> where TChunkDims : IChunkDims
{

    /// <summary>DDA Raycast over the entire cluster until the ray hits a block or leaves <see cref="activeChunks"/>.</summary>
    /// <param name="pos">Starting position of the ray.</param>
    /// <param name="dir">Direction the ray will travel.</param>
    /// <returns>
    /// <see cref="RaycastResult"/> with data filled on hit, if no hit accured returns with air and default data.
    /// </returns>
    public RaycastResult Raycast(Vector3 pos, Vector3 dir)
    {
        dir = Vector3.Normalize(dir);
        RaymarchHandler responder = new(this);
        responder = MarchChunks<RaymarchHandler, TChunkDims>(pos, dir, responder);
        return responder.result;
    }

    private struct RaymarchHandler(ChunkCluster<TChunkDims> clsuter) : IChunkMarchHandler
    {
        static readonly RaycastResult defaultResult = new(0, default, default, 0);
        public RaycastResult result = defaultResult;
        public ushort block;
        int chunkIndex;

        public bool OnBlockStep(Vector3D<int> blockPosition)
        {
            block = clsuter.flattenedChunks[ChunkMath<TChunkDims>.IndexByGlobalPos(blockPosition) + chunkIndex];
            return block == 0;
        }

        public bool OnChunkEntered(Vector3D<int> chunkPosition)
        {
            chunkIndex = clsuter.IndexByChunkCoord(clsuter.ChunkCoordByGlobalPos(chunkPosition));
            return clsuter.IsActive(chunkPosition);
        }

        public void OnComplete(Vector3D<int> blockPosition, Direction enteredFace, float distance) =>
            result = block != 0
                ? new(block, blockPosition, enteredFace.ToVector().Floor(), distance)
                : defaultResult;

        public bool OnInitialize(Vector3D<int> chunkPosition, Vector3D<int> blockPosition) => OnChunkEntered(chunkPosition);
    }
}