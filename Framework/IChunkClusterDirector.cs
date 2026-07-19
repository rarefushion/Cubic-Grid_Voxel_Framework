using GalensUnified.CubicGrid.Renderer.NET;
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
    /// <summary>Calculate chunk removals, additions and progresses generation pipeline.</summary>
    /// <param name="handler">The handler invoked on chunk updates.</param>
    /// <param name="frustum">The active view frustum. Defaults to null.</param>
    void ProcessChunks<THandler>(THandler handler, MatrixPlanes.Plane[]? frustum = null) where THandler : struct, IChunkDirectorUpdateHandler;
}