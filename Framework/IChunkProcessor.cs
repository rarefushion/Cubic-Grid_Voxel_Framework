namespace GalensUnified.CubicGrid.Framework;

/// <summary>A chunk's task that can be ran and tracked.</summary>
public delegate Task ChunkTask<TChunkKey>(TChunkKey chunk, int stage);

public abstract record ChunkTaskType
{
    /// <summary>Represents a task that will be ran on the main thread.</summary>
    public record Synchronous<TChunkKey>(ChunkTask<TChunkKey> ChunkTask) : ChunkTaskType;
    /// <summary>Represents a task that will be multi-threaded.</summary>
    public record Async<TChunkKey>(ChunkTask<TChunkKey> ChunkTask) : ChunkTaskType;
}

/// <summary>Defines how to create a chunk.</summary>
public interface IChunkProcessor<TChunkKey>
{
    /// <summary>Number of stages.</summary>
    int StagesCount { get; }
    /// <summary>The <see cref="ChunkTaskType"/> for the given chunk and it's stage.</summary>
    ChunkTaskType GetChunkTask(TChunkKey chunk, int stage);
}