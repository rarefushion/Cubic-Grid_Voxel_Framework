namespace GalensUnified.CubicGrid.Framework;


/// <summary>Information about a chunk that is generating.</summary>
/// <param name="Chunk">The key identifying the chunk.</param>
/// <param name="Stage">The chunk's current generation stage.</param>
public abstract record ChunkGenerationState<TChunkKey>(TChunkKey Chunk, int Stage)
{
    /// <summary>The <paramref name="Chunk"/> is processing <paramref name="Task"/>.</summary>
    public record Processing(TChunkKey Chunk, int Stage, Task Task) : ChunkGenerationState<TChunkKey>(Chunk, Stage);
    /// <summary>The <paramref name="Chunk"/> has finished generating.</summary>
    public record Finalized(TChunkKey Chunk, int Stage) : ChunkGenerationState<TChunkKey>(Chunk, Stage);
}

/// <summary>
/// Interface for managing a multi-stage chunk generation pipeline.<br/>
/// Chunks are registered and iterated through stages, with each stage represented by a Task.<br/>
/// Manual iteration allows for control over processing time.
/// </summary>
/// <typeparam name="TChunkKey">The type used to identify chunks, must be non-nullable and implement IEquatable.</typeparam>
public interface IChunkGenerationPipeline<TChunkKey> where TChunkKey : notnull, IEquatable<TChunkKey>
{
    /// <summary>Chunks currently in the pipeline processing.</summary>
    IEnumerable<ChunkGenerationState<TChunkKey>> ChunksInPipeline { get; }

    /// <summary>Registers a chunk into the pipeline, starting it's first Task</summary>
    void StartChunk(TChunkKey toStart);

    /// <summary>Iterate chunks that are generating ensuring they get processed.</summary>
    /// <returns>Chunks that are generating and their current state.</returns>
    IEnumerable<ChunkGenerationState<TChunkKey>> ProcessChunks();
}