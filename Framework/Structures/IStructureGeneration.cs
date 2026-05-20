using System.Numerics;
using Silk.NET.Maths;

namespace GalensUnified.CubicGrid.Framework.Structures;

/// <summary>The structure generation algorithm for a given structure type.</summary>
/// <remarks>
/// For any given chunk generate in this order:
/// <list type="number">
/// <item><see cref="PossibleChunks"/>
/// Obtain the chunk positions that could overlap the generating chunk.</item>
/// <item><see cref="FindChunksStructures"/>
/// Obtain all structure data for each of those chunks.</item>
/// <item><see cref="GetBlock"/>
/// For every block in the generating chunk
/// > and every <see cref="GeneratedStructureData"/> found
/// > query whether a block is placed here.<br/>
/// The position queried must be local to <see cref="GeneratedStructureData.LocalRootPosition"/>,
/// use <see cref="GeneratedStructureData.LocalPositionByGlobalPos(Vector3D{int})"/> to convert a global block position.
/// </item>
/// </list>
/// </remarks>
public interface IStructureGeneration
{
    /// <summary>Finds the chunks whose structures might overlap <paramref name="ChunkPosition"/>.</summary>
    /// <returns>An array of global chunk positions that might contain structures overlapping <paramref name="ChunkPosition"/>.</returns>
    Vector3D<int>[] PossibleChunks(Vector3D<int> ChunkPosition);
    /// <summary>Determine what structures generate in a chunk.</summary>
    GeneratedStructureData[] FindChunksStructures(Vector3D<int> ChunkPosition);
    /// <summary>Gets the block at a given local position.</summary>
    /// <param name="LocalPosition">The block position relative to the structure's root.</param>
    ushort GetBlock(Vector3D<int> LocalPosition);
}