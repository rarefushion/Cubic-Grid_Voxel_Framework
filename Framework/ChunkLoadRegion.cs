using GalensUnified.CubicGrid.Core.Math;
using Silk.NET.Maths;

namespace GalensUnified.CubicGrid.Framework;

public class ChunkLoadRegion(Vector3D<int> centrePosition, int chunkLength, int halfLengthInChunks) : IChunkLoadRegion
{
    public int ChunkLength => chunkLength;
    private int _halfLengthInChunks = halfLengthInChunks;
    public int HalfLengthInChunks => _halfLengthInChunks;
    private Vector3D<int> _centrePosition = centrePosition;
    public Vector3D<int> CentrePosition => _centrePosition;


    public Vector3D<int>[] chunks = [.. CubicNeighborhood.ExpandingCubePositions(centrePosition, new(halfLengthInChunks * chunkLength), chunkLength)];
    public event Action<Vector3D<int>>? ChunkAdded;
    public event Action<Vector3D<int>>? ChunkRemoved;

    public void SetPosition(Vector3D<int> centrePosition)
    {
        _centrePosition = centrePosition;
        UpdateManagedChunks();
    }

    public void SetLoadDistance(int halfLengthInChunks)
    {
        _halfLengthInChunks = halfLengthInChunks;
        UpdateManagedChunks();
    }

    private void UpdateManagedChunks()
    {
        Vector3D<int>[] newPositions = [.. CubicNeighborhood.ExpandingCubePositions(CentrePosition, new(HalfLengthInChunks * chunkLength), chunkLength)];
        List<Vector3D<int>> keysToRemove = [];
        foreach (Vector3D<int> pos in chunks)
            if (!newPositions.Contains(pos)) 
                keysToRemove.Add(pos);

        foreach (Vector3D<int> pos in keysToRemove)
            ChunkRemoved?.Invoke(pos);
        foreach (Vector3D<int> pos in newPositions)
            if (!chunks.Contains(pos))
                ChunkAdded?.Invoke(pos);
        chunks = newPositions;
    }
}