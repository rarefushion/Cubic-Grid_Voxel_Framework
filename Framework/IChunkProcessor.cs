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

public abstract record ChunkTaskGate
{
    /// <summary>Ready for the stage in question.</summary>
    public record Proceed() : ChunkTaskGate;
    /// <summary>Don't prceed with generation and the explicit reason.</summary>
    public abstract record Halt : ChunkTaskGate
    {
        public record Complete() : Halt;
    }
}

/// <summary>Defines how to create a chunk.</summary>
public interface IChunkProcessor<TChunkKey>
{
    /// <summary>The <see cref="ChunkTaskGate"/> for the given chunk and it's stage.</summary>
    ChunkTaskGate GetChunkTaskGate(TChunkKey chunk, int stage);
    /// <summary>The <see cref="ChunkTaskType"/> for the given chunk and it's stage.</summary>
    ChunkTaskType GetChunkTask(TChunkKey chunk, int stage);
}