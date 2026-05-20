using Silk.NET.Maths;

namespace GalensUnified.CubicGrid.Framework.Structures;

/// <summary>
/// Data about a generated structure.
/// Currently only it's position,
/// the type is implied from the <see cref="IStructureGeneration"/> it was obtained from,
/// the blocks are obtained via <see cref="IStructureGeneration.GetBlock(Vector3D{int})"/>.
/// </summary>
public readonly struct GeneratedStructureData(Vector3D<int> chunkPosition, Vector3D<int> localRootPosition)
{
    /// <summary>The chunk this structure's root resides within.</summary>
    public readonly Vector3D<int> ChunkPosition = chunkPosition;
    /// <summary>The local position within the chunk this structure is located.</summary>
    public readonly Vector3D<int> LocalRootPosition = localRootPosition;
    /// <summary>Convert a global position into a position that is local to this structure's root.</summary>
    public readonly Vector3D<int> LocalPositionByGlobalPos(Vector3D<int> globalPos) => globalPos - (ChunkPosition + LocalRootPosition);
}