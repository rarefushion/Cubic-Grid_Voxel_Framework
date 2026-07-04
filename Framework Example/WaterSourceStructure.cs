using System.Collections.Frozen;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Core.Math;
using GalensUnified.CubicGrid.Framework.Structures;
using Silk.NET.Maths;

using static BlockIDs;
using static GalensUnified.CubicGrid.Core.Math.DeterministicRandom;

public class WaterSourceStructure<TChunkDims> : IStructureGeneration
    where TChunkDims : IChunkDims
{
    const int PossibleSpawnEveryXBlocks = 32;
    const float SpawnChance = 0.05f;
    public static int GetHeight => 1;
    public static readonly Vector3D<int> MinBounds;
    public static readonly Vector3D<int> MaxBounds;
    // public static readonly FrozenDictionary<Vector3D<int>, ushort> blockByLocalPos;

    public Vector3D<int>[] PossibleChunks(Vector3D<int> ChunkPosition)
    {
        if (ChunkPosition.Y < 0)
            return [];
        return [ChunkPosition];
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
        if (LocalPosition == Vector3D<int>.Zero)
            return WaterFull;
        return 0;
    }
}