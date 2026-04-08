using Silk.NET.Maths;

namespace GalensUnified.CubicGrid.Framework;

public interface IChunkClusterRegistry
{
    public record ChunkUpdate(bool IsActive, Vector3D<int> Position);

    public bool IsProcessing { get; }
    public int ChunkLength { get; }
    public int HalfLengthInChunks { get; }
    public Vector3D<int> CentrePosition { get; }

    public IEnumerable<ChunkUpdate> SetPosition(Vector3D<int> centrePosition);
    public IEnumerable<ChunkUpdate> SetLoadDistance(int halfLengthInChunks);
    public IEnumerable<Vector3D<int>> GetLoadedChunks();
}