using Silk.NET.Maths;

namespace GalensUnified.CubicGrid.Framework;

public interface IChunkClusterRegistry
{
    public Vector3D<int> CentrePosition { get; }
    public int ChunkLength { get; }
    public int HalfLengthInChunks { get; }
    public event Action<Vector3D<int>> ChunkAdded;
    public event Action<Vector3D<int>> ChunkRemoved;

    public void SetPosition(Vector3D<int> centrePosition);
    public void SetLoadDistance(int halfLengthInChunks);
    public IEnumerable<Vector3D<int>> GetLoadedChunks();
}