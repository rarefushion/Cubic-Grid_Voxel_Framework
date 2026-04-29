using Silk.NET.Maths;

namespace GalensUnified.CubicGrid.Framework;

/// <summary>Represents a change within a chunk Director.</summary>
public abstract record ChunkDirectorUpdate(Vector3D<int> Chunk)
{
    /// <summary>The chunk is generating <see cref="Stage"/>.</summary>
    public record Generating(Vector3D<int> Chunk, int Stage) : ChunkDirectorUpdate(Chunk);
    /// <summary>The chunk has finished generating.</summary>
    public record GenerationComplete(Vector3D<int> Chunk) : ChunkDirectorUpdate(Chunk);
    /// <summary>The Director has deregistered the chunk.</summary>
    public record Deactivated(Vector3D<int> Chunk) : ChunkDirectorUpdate(Chunk);
}

/// <summary>Tracks which chunks are active and unloads chunks that are out of bounds.</summary>
public interface IChunkClusterDirector
{
    /// <summary>False if all chunks have been found, generated and out of bounds chunks have been evicted.</summary>
    public bool IsProcessing { get; }
    public int ChunkLength { get; }
    public int HalfLengthInChunks { get; }
    public Vector3D<int> CentrePosition { get; }

    IEnumerable<ChunkDirectorUpdate> Registry { get; }

    void SetCentrePosition(Vector3D<int> centrePosition);
    void SetLoadDistance(int halfLengthInChunks);
    /// <summary>Calculates the difference in the chunk boundry and progesses generation pipeline.</summary>
    /// <returns>ChunkUpdate representing a chunk state change.</returns>
    IEnumerable<ChunkDirectorUpdate> ProcessChunks();
}