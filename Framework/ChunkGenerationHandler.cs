using Silk.NET.Maths;

namespace GalensUnified.CubicGrid.Framework;

public enum ChunkGenerationStage
{
    Instantiated,
    PointsCalculated, // Individual blocks that don't need any information about block neighbors.
    ChunkNeighborsPointsCalculated, // Face, Side and Corner neighbors of this chunk completed their PointsCalculated stage.
    Rendered
}

/// <summary>
/// Tracks and advances the generation stage of a single chunk.
/// Fires events when the stage progresses or when the chunk is flagged for priority processing.
/// </summary>
/// <param name="position">The world position of the chunk this handler is responsible for.</param>
public class ChunkGenerationHandler(Vector3D<int> position)
{
    public readonly Vector3D<int> position = position;
    private ChunkGenerationStage _stage = ChunkGenerationStage.Instantiated;
    public ChunkGenerationStage Stage => _stage;

    /// <summary>Encapsulates the chunk's position and current stage for event callbacks.</summary>
    public record EventArgs(Vector3D<int> Position, ChunkGenerationStage Stage);
    /// <summary>Raised when the chunk advances to a new generation stage.</summary>
    public event Action<EventArgs>? StageUpdated;
    /// <summary>Raised when this chunk requests priority in the generation pipeline.</summary>
    public event Action<EventArgs>? PriorityRequested;

    /// <summary>Marks a generation stage as completed and advances the chunk's stage.</summary>
    /// <param name="completedStage">The stage being completed. Must be exactly one stage ahead of the current stage.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="completedStage"/> is not the immediate next stage.</exception>
    public void StageCompleted(ChunkGenerationStage completedStage)
    {
        if (completedStage > _stage + 1)
            throw new ArgumentException($"Cannot complete stage {completedStage} before completing chunk's next stage {_stage + 1}.");
        if (completedStage <= _stage)
            throw new ArgumentException($"Stage {completedStage} was already completed for this chunk. Current stage: {_stage}.");

        _stage = completedStage;
        StageUpdated?.Invoke(new(position, _stage));
    }
    /// <summary>Signals that this chunk should be prioritized in the generation pipeline.</summary>
    public void Prioritize() =>
        PriorityRequested?.Invoke(new(position, _stage));
}