namespace GalensUnified.CubicGrid.Framework;

/// <summary>Represents a chunk that is generating.</summary>
/// <param name="Chunk">The key identifying the chunk.</param>
/// <param name="Stage">The current stage of the chunk.</param>
/// <param name="Task">The Task currently processing the stage for this chunk.</param>
public record ChunkGenerating<TChunkKey>(TChunkKey Chunk, int Stage, Task Task);

/// <summary>
/// Interface for managing a multi-stage chunk generation pipeline.<br/>
/// Chunks are registered and iterated through stages, with each stage represented by a Task.<br/>
/// Manual iteration allows for control over processing time.
/// </summary>
/// <typeparam name="TChunkKey">The type used to identify chunks, must be non-nullable and implement IEquatable.</typeparam>
public interface IChunkGenerationPipeline<TChunkKey> where TChunkKey : notnull, IEquatable<TChunkKey>
{
    /// <summary>Number of stages.</summary>
    int StagesCount { get; }

    /// <summary>Chunks currently in the pipeline processing.</summary>
    IEnumerable<ChunkGenerating<TChunkKey>> ChunksInPipeline { get; }

    /// <summary>Registers a chunk into the pipeline, starting it's first Task</summary>
    void StartChunk(TChunkKey toStart);

    /// <summary>Iterate chunks with completed tasks. Starting next stage task or evicting completed.</summary>
    /// <returns>Chunks that have completed a stage during this call.</returns>
    IEnumerable<ChunkGenerating<TChunkKey>> ProcessChunks();
}