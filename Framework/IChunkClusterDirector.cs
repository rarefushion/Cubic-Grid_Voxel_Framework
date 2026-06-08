using Silk.NET.Maths;

namespace GalensUnified.CubicGrid.Framework;

/// <summary>Tracks which chunks are active and unloads chunks that are out of bounds.</summary>
public interface IChunkClusterDirector
{
    /// <summary>False if all chunks have been found, generated and out of bounds chunks have been evicted.</summary>
    public bool IsProcessing { get; }
    public int ChunkLength { get; }
    public int HalfLengthInChunks { get; }
    public Vector3D<int> CentrePosition { get; }

    IEnumerable<Vector3D<int>> Registry { get; }

    void SetCentrePosition(Vector3D<int> centrePosition);
    void SetLoadDistance(int halfLengthInChunks);
    /// <summary>Calculates the difference in the chunk boundry and progesses generation pipeline.</summary>
    void ProcessChunks<THandler>(THandler handler) where THandler : struct, IChunkDirectorUpdateHandler;
}