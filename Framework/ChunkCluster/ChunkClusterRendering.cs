using Silk.NET.Maths;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Renderer.NET;

namespace GalensUnified.CubicGrid.Framework;

public partial class ChunkCluster
{
    public Vector3D<int> NeighborPos(Vector3D<int> rootChunk, Direction direction) =>
        rootChunk + (direction.ToVector() * chunkLength).Floor();

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
        negZChunk = CheckedGetChunk(NeighborPos(rootChunk, Direction.Back));
        posZChunk = CheckedGetChunk(NeighborPos(rootChunk, Direction.Front));
        posYChunk = CheckedGetChunk(NeighborPos(rootChunk, Direction.Top));
        negYChunk = CheckedGetChunk(NeighborPos(rootChunk, Direction.Bottom));
        negXChunk = CheckedGetChunk(NeighborPos(rootChunk, Direction.Left));
        posXChunk = CheckedGetChunk(NeighborPos(rootChunk, Direction.Right));
    }

    /// <summary>
    /// Creates the Render Instances for any block that is visible.
    /// Gets the <paramref name="chunk"/>'s neighbors for culling.
    /// If a neighbor isn't in <see cref="acitveChunks"/> it will be assumed that all of the blocks on that side are visible.
    /// </summary>
    public FaceInstance[] CullChunk(Vector3D<int> chunk)
    {
        Span<ushort> rootChunkBlocks, negZChunk, posZChunk, posYChunk, negYChunk, negXChunk, posXChunk;
        rootChunkBlocks = GetChunkByPosition(chunk);
        GetChunkNeighbors(chunk, out negZChunk, out posZChunk, out posYChunk, out negYChunk, out negXChunk, out posXChunk);
        return BlockCulling.CullChunk(rootChunkBlocks, chunkLength, negZChunk, posZChunk, posYChunk, negYChunk, negXChunk, posXChunk);
    }
}