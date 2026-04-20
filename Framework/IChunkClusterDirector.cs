using Silk.NET.Maths;

namespace GalensUnified.CubicGrid.Framework;

/// <summary>Represents a change within a chunk Director.</summary>
/// <param name="IsActive">True if chunk is in the Director.</param>
/// <param name="IsGenerating">True if chunk is in generation pipeline.</param>
/// <param name="Stage">
/// The current stage of this chunk in it's generation pipeline.
/// Should be max if <paramref name="IsGenerating"/> is false.
/// </param>
/// <param name="Position">Position of the chunk.</param>
public record ChunkDirectorUpdate(bool IsActive, bool IsGenerating, int Stage, Vector3D<int> Position);

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