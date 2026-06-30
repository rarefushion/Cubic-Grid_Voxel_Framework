using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Core.Math;
using Silk.NET.Maths;

namespace GalensUnified.CubicGrid.Framework;

public partial class ChunkCluster<TChunkDims> where TChunkDims : IChunkDims
{
    /// <summary>Marker interface for per-instance block state.</summary>
    /// <remarks>Implementations must be class, not struct. One instance per block that has state.</remarks>
    public interface IBlockData;
    private readonly IBlockData[] flattenedChunkBlockData;

    /// <summary>Generic update payload passed to <see cref="IBlockBehavior.Update{TData}"/>.</summary>
    /// <typeparam name="TPayload">Type of the payload data carried by this update.</typeparam>
    /// <param name="Payload">The payload carried to the behavior.</param>
    /// <remarks>
    /// When you receive an update these patterns can be useful:<br/>
    /// 1. switch (blockUpdate.Payload) { case int i: i++; break; }<br/>
    /// 2. if (letter.Payload is not int payload) { return; } payload++;
    /// </remarks>
    public record BlockUpdate<TPayload>(TPayload Payload);

    /// <summary>
    /// Behavior for a given block type.
    /// One instance per block type, registered in <see cref="blockBehaviorByBlock"/>.
    /// </summary>
    /// <remarks>
    /// Behaviors are invoked by <see cref="UpdateBlockData{TData}"/> when the user
    /// triggers an update on a block position. The behavior receives the block's position,
    /// its <see cref="IBlockData"/>, and a <see cref="BlockUpdate{TPayload}"/> that describes the event.
    /// </remarks>
    public interface IBlockBehavior
    {
        /// <summary>Called when an update is signalled to a block of this type.</summary>
        /// <typeparam name="TPayload">Type of the update payload. Use pattern matching to branch on the concrete type.</typeparam>
        /// <param name="blockPosition">Global position of the block being updated.</param>
        /// <param name="blockData">The block's per-instance data.</param>
        /// <param name="blockUpdate">The update payload with its unique data.</param>
        /// <remarks>
        /// These patterns can be useful:<br/>
        /// 1. switch (blockUpdate.Payload) { case int i: i++; break; }<br/>
        /// 2. if (letter.Payload is not int payload) { return; } payload++;
        /// </remarks>
        void Update<TPayload>(Vector3D<int> blockPosition, IBlockData blockData, BlockUpdate<TPayload> blockUpdate);
    }
    private readonly IBlockBehavior?[] blockBehaviorByBlock;

    /// <summary>Attempts to retrieve typed block data at <paramref name="blockPosition"/>.</summary>
    /// <typeparam name="TBlockData">The expected type of the block data. Must be a class implementing <see cref="IBlockData"/>.</typeparam>
    /// <param name="blockPosition">Global position of the block.</param>
    /// <param name="blockData">The retrieved data cast to <typeparamref name="TBlockData"/>, or null if not found.</param>
    /// <returns>true if data exists at the position and the chunk is active; otherwise false.</returns>
    public bool TryGetBlockData<TBlockData>(Vector3D<int> blockPosition, out TBlockData? blockData)
    where TBlockData : class, IBlockData
    {
        Vector3D<int> chunkPosition = blockPosition.FloorTo(TChunkDims.Length);
        blockData = null;
        if (!IsActive(chunkPosition))
            return false;
        int chunkIndex = IndexByChunkCoord(ChunkCoordByGlobalPos(blockPosition));
        int blockIndex = ChunkMath<TChunkDims>.IndexByGlobalPos(blockPosition);
        if (flattenedChunkBlockData[chunkIndex + blockIndex] == null)
            return false;
        blockData = (TBlockData)flattenedChunkBlockData[chunkIndex + blockIndex];
        return true;
    }

    /// <summary>Sets or replaces <paramref name="blockData"/> at <paramref name="blockPosition"/>.</summary>
    /// <typeparam name="TBlockData">The type of the block data. Must be a class implementing <see cref="IBlockData"/>.</typeparam>
    /// <param name="blockPosition">Global position of the block.</param>
    /// <param name="blockData">The data instance to store. Must not be null.</param>
    /// <exception cref="ChunkInactiveException">The chunk containing <paramref name="blockPosition"/> is not active.</exception>
    public void SetBlockData<TBlockData>(Vector3D<int> blockPosition, TBlockData blockData)
    where TBlockData : class, IBlockData
    {
        Vector3D<int> chunkPosition = blockPosition.FloorTo(TChunkDims.Length);
        int chunkIndex = IndexByChunkCoord(ChunkCoordByGlobalPos(blockPosition));
        if (!IsActive(chunkPosition))
            throw new ChunkInactiveException(chunkPosition, chunkIndex, $"Requested block data in a chunk that is not active.");
        int blockIndex = ChunkMath<TChunkDims>.IndexByGlobalPos(blockPosition);
        flattenedChunkBlockData[chunkIndex + blockIndex] = blockData;
    }

    /// <summary>
    /// Signals an update to the block at <paramref name="blockPosition"/>. Reads the block ID,
    /// looks up its <see cref="IBlockBehavior"/> in <see cref="blockBehaviorByBlock"/>,
    /// retrieves the block's <see cref="IBlockData"/>, and invokes <see cref="IBlockBehavior.Update{TData}"/>.
    /// </summary>
    /// <typeparam name="TData">Type of the update payload carried by <paramref name="blockUpdate"/>.</typeparam>
    /// <param name="blockPosition">Global position of the block to update.</param>
    /// <param name="blockUpdate">The update payload.</param>
    /// <exception cref="ChunkInactiveException">The chunk containing <paramref name="blockPosition"/> is not active.</exception>
    /// <exception cref="BlockDataNullReferenceException">No block data has been assigned to this block.</exception>
    /// <exception cref="BlockBehaviorNullReferenceException">
    /// The block ID at <paramref name="blockPosition"/> has no behavior registered in <see cref="blockBehaviorByBlock"/>,
    /// or the registered slot is null.
    /// </exception>
    public void UpdateBlockData<TData>(Vector3D<int> blockPosition, BlockUpdate<TData> blockUpdate)
    {
        Vector3D<int> chunkPosition = blockPosition.FloorTo(TChunkDims.Length);
        int chunkIndex = IndexByChunkCoord(ChunkCoordByGlobalPos(blockPosition));
        if (!IsActive(chunkPosition))
            throw new ChunkInactiveException(chunkPosition, chunkIndex, $"Requested block data in a chunk that is not active.");

        int blockIndex = ChunkMath<TChunkDims>.IndexByGlobalPos(blockPosition);
        IBlockData blockData = flattenedChunkBlockData[chunkIndex + blockIndex]
            ?? throw new BlockDataNullReferenceException(blockPosition, $"Requested block data that has not been assigned.");

        int block = flattenedChunks[chunkIndex + blockIndex];
        if (blockBehaviorByBlock.Length <= block)
            throw new BlockBehaviorNullReferenceException(block, $"Block({block}) has no behavior assigned.");
        IBlockBehavior blockBehavior = blockBehaviorByBlock[block]
            ?? throw new BlockBehaviorNullReferenceException(block, $"Block({block}) has no behavior assigned.");

        blockBehavior.Update(blockPosition, blockData, blockUpdate);
    }

    /// <summary>Thrown when block data is requested for a block that has no <see cref="IBlockData"/> assigned.</summary>
    public class BlockDataNullReferenceException(Vector3D<int> blockPosition, string message) : Exception(message);

    /// <summary>Thrown when a block ID has no <see cref="IBlockBehavior"/> registered in <see cref="blockBehaviorByBlock"/>.</summary>
    public class BlockBehaviorNullReferenceException(int block, string message) : Exception(message);
}