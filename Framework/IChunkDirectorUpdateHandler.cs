using Silk.NET.Maths;

namespace GalensUnified.CubicGrid.Framework;

public interface IChunkDirectorUpdateHandler
{
    /// <summary>Callback on chunk generation <see cref="Stage"/> change.</summary>
    /// <returns>False to pause processing.</returns>
    public bool OnGenerationUpdate(Vector3D<int> Chunk, int Stage);
    /// <summary>The chunk has finished generating.</summary>
    /// <param name="Cullable">All neighbors on this chunks face have finished generating.</param>
    /// <param name="CullNeighbors">These neighbors have all of their neighbors finished generating</param>
    /// <remarks>Cullable is shorthand for all neighbors are generated.</remarks>
    /// <returns>False to pause processing.</returns>
    public bool OnGenerationComplete(Vector3D<int> Chunk, bool Cullable, Vector3D<int>[] CullNeighbors);
    /// <summary>The Director has deregistered the chunk.</summary>
    /// <returns>False to pause processing.</returns>
    public bool OnDeactivated(Vector3D<int> Chunk);
}