using GalensUnified.CubicGrid.Core;
using Silk.NET.Maths;

namespace GalensUnified.CubicGrid.Framework;

/// <summary>
/// Manages a specified number of chunks allowing retrieval via their position.<br/>
/// Uses one flattened array of ushorts for every chunk.
/// Indexes are generate to represent a position,
/// the positions are first wrapped to be contained within the cluster.
/// E.g a chunk at X:0, Y:0 Z:0 will have the same index as a chunk at X:(the cluster's length) Y:0 Z:0.
/// You must make sure chunks with the same index are removed before adding the next.<br/>
/// You can completely bypass the positional error checking by using <see cref="GetChunkByIndex"/>.
/// </summary>
public partial class ChunkCluster<TChunkDims> where TChunkDims : IChunkDims
{
    public readonly int chunkCount;
    public readonly int clusterChunkLength;
    public readonly int clusterChunkHeight;
    public readonly int clusterLength;
    public readonly int clusterHeight;
    public readonly int blockCount;

    private readonly Dictionary<int, Vector3D<int>> activeChunkPositionByIndex = [];

    private readonly ushort[] flattenedChunks;

    /// <summary>Fetches a chunk Span{ushort} that can be directly modified.</summary>
    /// <remarks>Use carefully, this bypasses the positional error checking.</remarks>
    private Span<ushort> GetChunkByIndex(int index) =>
        flattenedChunks.AsSpan(index, TChunkDims.Volume);

    /// <summary>Fetches a chunk Span{ushort} that can be directly modified.</summary>
    /// <remarks>Probably thread safe if each thread sticks to it's own chunk.</remarks>
    /// <exception cref="ChunkIndexCollisionException"/>
    public Span<ushort> GetChunkByPosition(Vector3D<int> pos)
    {
        int index = IndexByChunkCoord(ChunkCoordByGlobalPos(pos));
        if (activeChunkPositionByIndex.TryGetValue(index, out Vector3D<int> associatedPosition) && pos != associatedPosition)
            throw new ChunkIndexCollisionException
            (
                associatedPosition,
                pos,
                index,
                "There is another chunk with the same index that is active. " +
                "Remove the overlapping chunk before assigning the new one."
            );
        return GetChunkByIndex(index);
    }

    /// <summary>Fetches a chunk Span{ushort} that can be directly modified only if returned true.</summary>
    /// <returns>True if <paramref name="pos"/> is active. Else false, making <paramref name="chunk"/> empty.</returns>
    public bool TryGetChunk(Vector3D<int> pos, out Span<ushort> chunk)
    {
        if (IsActive(pos))
        {
            chunk = GetChunkByPosition(pos);
            return true;
        }
        else
        {
            chunk = [];
            return false;
        }
    }

    /// <summary>Registers a chunk as active.</summary>
    /// <remarks>Only tells the cluster it's active. To register blocks use <see cref="GetChunkByPosition"/>.</remarks>
    /// <exception cref="ChunkIndexCollisionException"/>
    public void EnableChunk(Vector3D<int> pos)
    {
        int index = IndexByChunkCoord(ChunkCoordByGlobalPos(pos));
        if (activeChunkPositionByIndex.TryGetValue(index, out Vector3D<int> associatedPosition) && pos != associatedPosition)
            throw new ChunkIndexCollisionException
            (
                activeChunkPositionByIndex[index],
                pos,
                index,
                "A chunk with the same index has already been added. " +
                "The new chunk produces the same index as a chunk that already exists. " +
                "Remove the overlapping chunk before adding the new one."
            );
        activeChunkPositionByIndex.Add(index, pos);
    }

    /// <summary>Sets the entire specified chunk to 0(Air) and de-registers.</summary>
    /// <returns>False if the chunk wasn't active or if the chunk that was removed isn't the chunk at <paramref name="pos"/>. Else true.</returns>
    /// <remarks>The chunk is cleared regardless of return result.</remarks>
    public bool TryRemoveChunk(Vector3D<int> pos)
    {
        bool toReturn = IsActive(pos);
        GetChunkByPosition(pos).Clear();
        activeChunkPositionByIndex.Remove(IndexByChunkCoord(ChunkCoordByGlobalPos(pos)));
        return toReturn;
    }

    /// <summary>Determines if the <paramref name="pos"/> is active and is the same one that was given to <see cref="EnableChunk"/>.</summary>
    public bool IsActive(Vector3D<int> pos) =>
        activeChunkPositionByIndex.TryGetValue(IndexByChunkCoord(ChunkCoordByGlobalPos(pos)), out Vector3D<int> associatedPosition) &&
        associatedPosition == pos;

    /// <summary>
    /// Calculates the chunk coordinate (grid address) by dividing a position by the chunk size.
    /// First wrapping the position into the local world space.
    /// </summary>
    public Vector3D<int> ChunkCoordByGlobalPos(Vector3D<int> pos) =>
        ChunkCoordByLocalPos(LocalPosByGlobalPos(pos));


    public Vector3D<int> LocalPosByGlobalPos(Vector3D<int> pos) => new
        (
            ((pos.X % clusterLength) + clusterLength) % clusterLength,
            ((pos.Y % clusterHeight) + clusterHeight) % clusterHeight,
            ((pos.Z % clusterLength) + clusterLength) % clusterLength
        );

    /// <summary>Calculates the chunk coordinate (grid address) by dividing a position by the chunk size.</summary>
    public Vector3D<int> ChunkCoordByLocalPos(Vector3D<int> pos) =>
        pos / TChunkDims.Length;

    /// <summary>Calculates the 1D index of a chunk coordinate (grid address).</summary>
    public int IndexByChunkCoord(Vector3D<int> coord) =>
        ((coord.Z * clusterChunkHeight + coord.Y) * clusterChunkLength + coord.X) * TChunkDims.Volume;

    /// <param name="chunkLength">The length of a single chunk. In other words the cube root of the chunk's volume.</param>
    /// <param name="clusterChunkLength">Number of chunks along each axis, allowing for a non cubic cluster.</param>
    public ChunkCluster(int clusterChunkLength, int clusterChunkHeight)
    {
        this.clusterChunkLength = clusterChunkLength;
        this.clusterChunkHeight = clusterChunkHeight;
        this.chunkCount = checked(clusterChunkLength * clusterChunkHeight * clusterChunkLength);
        this.clusterLength = clusterChunkLength * TChunkDims.Length;
        this.clusterHeight = clusterChunkHeight * TChunkDims.Length;
        this.blockCount = checked(TChunkDims.Volume * chunkCount);
        this.flattenedChunks = new ushort[blockCount];
    }

    /// <summary>Represents a collision where different positions produced the same index.</summary>
    /// <param name="existingChunk">The position of the chunk that already existed in the cluster.</param>
    /// <param name="collidingChunk">The position of the chunk that produces the same index as <paramref name="existingChunk"/>.</param>
    /// <param name="collidingIndex">The index that both chunk positions produce.</param>
    public class ChunkIndexCollisionException(Vector3D<int> existingChunk, Vector3D<int> collidingChunk, int collidingIndex, string message) : Exception(message);
}