using System.Collections.Frozen;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Core.Math;
using GalensUnified.CubicGrid.Framework.Structures;
using Silk.NET.Maths;

using static BlockIDs;
using static GalensUnified.CubicGrid.Core.Math.DeterministicRandom;

public class Tree<TChunkDims> : IStructureGeneration
    where TChunkDims : IChunkDims
{
    const int PossibleSpawnEveryXBlocks = 4;
    const float SpawnChance = 0.025f;
    public static int GetHeight => blockByLocalPos.Keys.Select(p => p.Y).Max();
    public static readonly Vector3D<int> MinBounds;
    public static readonly Vector3D<int> MaxBounds;
    public static readonly FrozenDictionary<Vector3D<int>, ushort> blockByLocalPos;

    public Vector3D<int>[] PossibleChunks(Vector3D<int> ChunkPosition)
    {
        // This structure is smaller than a chunk so only check immediate neighbors.
        // No chunks above ChunkPosition matter because this structure does not go down.
        int chunkTop = ChunkPosition.Y + TChunkDims.Length;
        if (chunkTop <= ChunkProcessor<TChunkDims>.MinTerrainHeight)
            return [];
        List<Vector3D<int>> toReturn = [];
        toReturn.Add(ChunkPosition);
        foreach (Vector3D<int> checkChunk in CubicNeighborhood.ExpandingCubePositions(ChunkPosition, new Vector3D<int>(TChunkDims.Length), TChunkDims.Length))
            if (ChunkPosition.Y >= checkChunk.Y)
                toReturn.Add(checkChunk);
        return [.. toReturn];
    }

    public GeneratedStructureData[] FindChunksStructures(Vector3D<int> ChunkPosition)
    {
        int chunkTop = ChunkPosition.Y + TChunkDims.Length;
        List<GeneratedStructureData> toReturn = [];
        for (int Z = 0; Z < TChunkDims.Length; Z++)
        for (int X = 0; X < TChunkDims.Length; X++)
        {
            Vector3D<int> localRootPosition = new(X, 0, Z);
            Vector3D<int> testPosition = localRootPosition + ChunkPosition;
            if (testPosition.Z % PossibleSpawnEveryXBlocks != 0 || testPosition.X % PossibleSpawnEveryXBlocks != 0)
                continue;
            int mountainHeight = ChunkProcessor<TChunkDims>.GetMountainHeight(testPosition);
            if (chunkTop <= mountainHeight || ChunkPosition.Y > mountainHeight)
                continue;
            testPosition.Y = mountainHeight;
            localRootPosition.Y = mountainHeight - ChunkPosition.Y;
            if (ChunkProcessor<TChunkDims>.IsErodid(testPosition))
                continue;

            if (NormalizedRandom(testPosition) < SpawnChance)
                toReturn.Add(new(ChunkPosition, localRootPosition));
        }

        return [.. toReturn];
    }

    public ushort GetBlock(Vector3D<int> LocalPosition)
    {
        if (blockByLocalPos.TryGetValue(LocalPosition, out ushort block))
            return block;
        return 0;
    }

    static Tree()
    {
        Vector3D<int> offset = Vector3D<int>.Zero;
        Dictionary<Vector3D<int>, ushort> blockByLocalPosition = [];
        // Height before leaves
        // start up one block, to leave root block unmodified
        for (offset.Y = 1; offset.Y < 5; offset.Y++)
            blockByLocalPosition[offset] = OakLog;
        blockByLocalPosition[offset] = OakLog;
        // Bottom layer of leaves
        offset.Y += 1;
        blockByLocalPosition[offset] = OakLog;
        foreach (Vector3D<int> flatNeighbor in CubicNeighborhood.MooreNeighborhood2D.Select(p => new Vector3D<int>(p.X, 0, p.Y)))
            blockByLocalPosition[offset + flatNeighbor] = OakLeaves;
        // Main cube of leaves
        int leafExtents = 2;
        int startOffset = offset.Y;
        for (/*offset.Y starts unchanged*/; offset.Y < startOffset + leafExtents + 1; offset.Y++)
            blockByLocalPosition[offset] = OakLog;
        blockByLocalPosition[offset] = OakLog;
        foreach (Vector3D<int> exactPos in CubicNeighborhood.ExpandingCubePositions(offset, new Vector3D<int>(leafExtents), 1))
            // if (cubeNeighbor.X != 0 && cubeNeighbor.Z != 0)
                blockByLocalPosition[exactPos] = OakLeaves;
        // -Z leaves
        offset.Z = -leafExtents - 1;
        blockByLocalPosition[offset] = OakLeaves;
        foreach (Vector3D<int> flatNeighbor in CubicNeighborhood.MooreNeighborhood2D.Select(p => new Vector3D<int>(p.X, p.Y, 0)))
            blockByLocalPosition[offset + flatNeighbor] = OakLeaves;
        // +Z leaves
        offset.Z = leafExtents + 1;
        blockByLocalPosition[offset] = OakLeaves;
        foreach (Vector3D<int> flatNeighbor in CubicNeighborhood.MooreNeighborhood2D.Select(p => new Vector3D<int>(p.X, p.Y, 0)))
            blockByLocalPosition[offset + flatNeighbor] = OakLeaves;
        offset.Z = 0;
        // -X leaves
        offset.X = -leafExtents - 1;
        blockByLocalPosition[offset] = OakLeaves;
        foreach (Vector3D<int> flatNeighbor in CubicNeighborhood.MooreNeighborhood2D.Select(p => new Vector3D<int>(0, p.X, p.Y)))
            blockByLocalPosition[offset + flatNeighbor] = OakLeaves;
        // +X leaves
        offset.X = leafExtents + 1;
        blockByLocalPosition[offset] = OakLeaves;
        foreach (Vector3D<int> flatNeighbor in CubicNeighborhood.MooreNeighborhood2D.Select(p => new Vector3D<int>(0, p.X, p.Y)))
            blockByLocalPosition[offset + flatNeighbor] = OakLeaves;
        offset.X = 0;
        // Remaining inner logs
        startOffset = offset.Y;
        for (/*offset.Y starts unchanged*/; offset.Y < startOffset + leafExtents; offset.Y++)
            blockByLocalPosition[offset] = OakLog;
        blockByLocalPosition[offset] = OakLog;
        // Top leaves
        offset.Y += 1;
        blockByLocalPosition[offset] = OakLeaves;
        foreach (Vector3D<int> flatNeighbor in CubicNeighborhood.MooreNeighborhood2D.Select(p => new Vector3D<int>(p.X, 0, p.Y)))
            blockByLocalPosition[offset + flatNeighbor] = OakLeaves;
        offset.Y += 1;
        blockByLocalPosition[offset] = OakLeaves;
        blockByLocalPos = blockByLocalPosition.ToFrozenDictionary();

        // Create Bounds
        MinBounds = Vector3D<int>.Zero;
        MaxBounds = Vector3D<int>.Zero;
        MinBounds.X = blockByLocalPosition.Keys.Select(p => p.X).Min();
        MaxBounds.X = blockByLocalPosition.Keys.Select(p => p.X).Max();
        MinBounds.Y = blockByLocalPosition.Keys.Select(p => p.Y).Min();
        MaxBounds.Y = blockByLocalPosition.Keys.Select(p => p.Y).Max();
        MinBounds.Z = blockByLocalPosition.Keys.Select(p => p.Z).Min();
        MaxBounds.Z = blockByLocalPosition.Keys.Select(p => p.Z).Max();
    }
}