using Silk.NET.Maths;

namespace GalensUnified.CubicGrid.Framework;

/// <summary>Manages a specified number of chunks allowing retrieval via their position.</summary>
public partial class ChunkCluster
{
    public readonly int chunkLength;
    public readonly int chunkVolume;
    public readonly int chunkCount;
    public readonly int clusterChunkLength;
    public readonly int clusterChunkHeight;
    public readonly int clusterLength;
    public readonly int clusterHeight;
    public readonly int blockCount;

    public readonly HashSet<Vector3D<int>> activeChunks = [];

    private readonly ushort[] flattenedChunks;

    /// <summary>Fetches a chunk (Span<ushort>) that can be directly modified.</summary>
    private Span<ushort> GetChunkByIndex(int index) =>
        flattenedChunks.AsSpan(index, chunkVolume);

    /// <summary>Fetches a chunk (Span<ushort>) that can be directly modified.</summary>
    public Span<ushort> GetChunkByPosition(Vector3D<int> pos) =>
        GetChunkByIndex(IndexByChunkCoord(ChunkCoordByGlobalPos(pos)));

    public void AddChunk(Vector3D<int> pos) =>
        activeChunks.Add(pos);

    /// <summary>Sets the entire specified chunk to Air.</summary>
    public void RemoveChunk(Vector3D<int> pos)
    {
        GetChunkByPosition(pos).Clear();
        activeChunks.Remove(pos);
    }

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
        pos / chunkLength;

    /// <summary>Calculates the 1D index of a chunk coordinate (grid address).</summary>
    public int IndexByChunkCoord(Vector3D<int> coord) =>
        ((coord.Z * clusterChunkHeight + coord.Y) * clusterChunkLength + coord.X) * chunkVolume;

    /// <param name="chunkLength">The length of a single chunk. In other words the cube root of the chunk's volume.</param>
    /// <param name="clusterChunkLength">Number of chunks along each axis, allowing for a non cubic cluster.</param>
    public ChunkCluster(int chunkLength, int clusterChunkLength, int clusterChunkHeight)
    {
        this.chunkLength = chunkLength;
        this.chunkVolume = chunkLength * chunkLength * chunkLength;
        this.clusterChunkLength = clusterChunkLength;
        this.clusterChunkHeight = clusterChunkHeight;
        this.chunkCount = checked(clusterChunkLength * clusterChunkHeight * clusterChunkLength);
        this.clusterLength = clusterChunkLength * chunkLength;
        this.clusterHeight = clusterChunkHeight * chunkLength;
        this.blockCount = checked(chunkVolume * chunkCount);
        this.flattenedChunks = new ushort[blockCount];
    }
}