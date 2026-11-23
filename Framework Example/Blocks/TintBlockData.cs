using System.Numerics;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Framework;

public class TintBlockData<TChunkDims>(Vector3 tint) : ChunkCluster<TChunkDims>.IBlockData
where TChunkDims : IChunkDims
{
    public readonly Vector3 tint = tint;
}