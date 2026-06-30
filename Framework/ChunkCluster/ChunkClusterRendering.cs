using Silk.NET.Maths;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Renderer.NET;

namespace GalensUnified.CubicGrid.Framework;

public partial class ChunkCluster<TChunkDims> where TChunkDims : IChunkDims
{
    public Vector3D<int> NeighborPos(Vector3D<int> rootChunk, Direction direction) =>
        rootChunk + (direction.ToVector() * TChunkDims.Length).Floor();

    /// <summary>Fetches all neighbors for this chunk. Empty span if not <see cref="IsActive"/>.</summary>
    public void GetChunkNeighbors
    (
        Vector3D<int> rootChunk,
        out Span<ushort> negZChunk,
        out Span<ushort> posZChunk,
        out Span<ushort> posYChunk,
        out Span<ushort> negYChunk,
        out Span<ushort> negXChunk,
        out Span<ushort> posXChunk
    )
    {
        TryGetChunk(NeighborPos(rootChunk, Direction.Back), out negZChunk);
        TryGetChunk(NeighborPos(rootChunk, Direction.Front), out posZChunk);
        TryGetChunk(NeighborPos(rootChunk, Direction.Top), out posYChunk);
        TryGetChunk(NeighborPos(rootChunk, Direction.Bottom), out negYChunk);
        TryGetChunk(NeighborPos(rootChunk, Direction.Left), out negXChunk);
        TryGetChunk(NeighborPos(rootChunk, Direction.Right), out posXChunk);
    }

    /// <summary>
    /// Creates the Render Instances for any block that is visible.
    /// Gets the <paramref name="chunk"/>'s neighbors for culling.
    /// If a neighbor isn't in <see cref="acitveChunks"/> it will be assumed that all of the blocks on that side are visible.
    /// </summary>
    public THandler CullChunk<THandler>(Vector3D<int> chunk, THandler handler)
    where THandler : struct, IBlockCullingHandler
    {
        Span<ushort> rootChunkBlocks, negZChunk, posZChunk, posYChunk, negYChunk, negXChunk, posXChunk;
        rootChunkBlocks = GetChunkByPosition(chunk);
        GetChunkNeighbors(chunk, out negZChunk, out posZChunk, out posYChunk, out negYChunk, out negXChunk, out posXChunk);
        return BlockCulling.CullChunk(rootChunkBlocks, handler, TChunkDims.Length, negZChunk, posZChunk, posYChunk, negYChunk, negXChunk, posXChunk);
    }
}