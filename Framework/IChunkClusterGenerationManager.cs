using System;
using Silk.NET.Maths;

namespace GalensUnified.CubicGrid.Framework;

/// <summary>A predicate evaluated during processes to allow the caller to signal early termination.</summary>
public delegate bool CancelationCondition();

public interface IChunkClusterGenerationManager
{
    /// <summary>Invoked when an error occurs during chunk processing. The string parameter contains a human-readable error message.</summary>
    Action<string>? ErrorThrown { get; set; }

    /// <summary>Invoked when a chunk finished processing a stage. The Vector3D{int} parameter contains the chunks position. The int parameter contains the stage that has just concluded.</summary>
    Action<Vector3D<int>, int>? StageCompleted { get; set; }

    /// <summary>Chunks currently in the pipeline, queued or processing.</summary>
    IEnumerable<Vector3D<int>> ChunksInPipeline { get; }

    /// <summary>Chunks currently being prioritized.</summary>
    IEnumerable<Vector3D<int>> ChunksPrioritized { get; }

    /// <summary>Performs one full pass of the generation pipeline: advances completed tasks to their next stage, then queues new work.</summary>
    /// <param name="cancelationCondition">Called to allow the caller to abort mid-pass if it becomes <c>true</c>..</param>
    void ProcessChunks(CancelationCondition cancelationCondition);

    /// <summary>Registers a chunk into the pipeline.</summary>
    /// <param name="pos">The position of the chunk to enqueue.</param>
    void EnqueueChunk(Vector3D<int> pos);

    /// <summary>Evicts a chunk from the pipeline.</summary>
    /// <param name="pos">The position of the chunk to discard.</param>
    void DiscardChunk(Vector3D<int> pos);

    /// <summary>Marks a chunk to have process priority over others.</summary>
    /// <param name="pos">The position of the chunk to prioritize.</param>
    void PrioritizeChunk(Vector3D<int> pos);
}
