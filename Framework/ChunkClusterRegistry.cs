using GalensUnified.CubicGrid.Core.Math;
using Silk.NET.Maths;

using ChunkUpdate = GalensUnified.CubicGrid.Framework.IChunkClusterRegistry.ChunkUpdate;

namespace GalensUnified.CubicGrid.Framework;

public class ChunkClusterRegistry(Vector3D<int> centrePosition, int chunkLength, int halfLengthInChunks) : IChunkClusterRegistry
{
    public bool IsProcessing { get; private set; } = false;
    public int ChunkLength { get; private set; } = chunkLength;
    public int HalfLengthInChunks { get; private set; } = halfLengthInChunks;
    public Vector3D<int> CentrePosition { get; private set; } = centrePosition;


    public HashSet<Vector3D<int>> chunks = [];

    public IEnumerable<ChunkUpdate> SetPosition(Vector3D<int> centrePosition)
    {
        CentrePosition = centrePosition;
        return UpdateManagedChunks();
    }

    public IEnumerable<ChunkUpdate> SetLoadDistance(int halfLengthInChunks)
    {
        HalfLengthInChunks = halfLengthInChunks;
        return UpdateManagedChunks();
    }

    public IEnumerable<Vector3D<int>> GetLoadedChunks() =>
        chunks;

    private IEnumerable<ChunkUpdate> UpdateManagedChunks()
    {
        IsProcessing = true;
        HashSet<Vector3D<int>> prevSet = [.. chunks];
        foreach (Vector3D<int> chunk in CubicNeighborhood.ExpandingCubePositions(CentrePosition, new(HalfLengthInChunks * ChunkLength), ChunkLength))
        {
            if (!prevSet.Contains(chunk))
            {
                chunks.Add(chunk);
                yield return new(true, chunk);
            }
            else
                prevSet.Remove(chunk);
        }
        foreach (Vector3D<int> chunk in prevSet)
        {
            chunks.Remove(chunk);
            yield return new(false, chunk);
        }
        IsProcessing = false;
    }
}