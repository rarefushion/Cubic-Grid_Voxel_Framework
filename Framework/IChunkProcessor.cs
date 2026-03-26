using Silk.NET.Maths;

namespace GalensUnified.CubicGrid.Framework;

public interface IChunkProcessor
{
    Task CalculatePointsAsync(Vector3D<int> chunk);
    Task OnNeighborPointsCalculatedAsync(Vector3D<int> chunk);
    Task RenderAsync(Vector3D<int> chunk);
}