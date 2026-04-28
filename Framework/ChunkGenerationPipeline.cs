namespace GalensUnified.CubicGrid.Framework;

using HaltGen = ChunkTaskGate.Halt;

public class ChunkGenerationPipeline<TChunkKey>(IChunkProcessor<TChunkKey> processor) :
    IChunkGenerationPipeline<TChunkKey>
    where TChunkKey : notnull, IEquatable<TChunkKey>
{
    public IEnumerable<ChunkGenerationState<TChunkKey>> ChunksInPipeline => chunkByPos.Values;
    private readonly IChunkProcessor<TChunkKey> processor = processor;
    private readonly Dictionary<TChunkKey, ChunkGenerationState<TChunkKey>.Processing> chunkByPos = [];

    public void StartChunk(TChunkKey toStart) =>
        chunkByPos.Add(toStart, new ChunkGenerationState<TChunkKey>.Processing(toStart, 0, StartChunkTask(toStart, 0)));

    public IEnumerable<ChunkGenerationState<TChunkKey>> ProcessChunks()
    {
        ChunkGenerationState<TChunkKey>.Processing[] chunks = [.. chunkByPos.Values];
        foreach (ChunkGenerationState<TChunkKey>.Processing chunk in chunks)
            if (chunk.Task.IsCompleted)
            {
                if (!chunk.Task.IsCompletedSuccessfully)
                    throw new Exception($"Chunk {chunk.Chunk} failed to process stage {chunk.Stage}.", chunk.Task.Exception);

                int nextStage = chunk.Stage + 1;
                switch (processor.GetChunkTaskGate(chunk.Chunk, nextStage))
                {
                    case HaltGen.Complete:
                        chunkByPos.Remove(chunk.Chunk);
                        yield return new ChunkGenerationState<TChunkKey>.Finalized(chunk.Chunk, nextStage);
                        break;
                    case ChunkTaskGate.Proceed:
                        chunkByPos[chunk.Chunk] = chunk with
                        {
                            Stage = nextStage,
                            Task = StartChunkTask(chunk.Chunk, nextStage)
                        };
                        yield return chunkByPos[chunk.Chunk];
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }
    }

    public Task StartChunkTask(TChunkKey chunk, int stage) => processor.GetChunkTask(chunk, stage) switch
    {
        ChunkTaskType.Async<TChunkKey> async =>  Task.Run(() => async.ChunkTask(chunk, stage)),
        ChunkTaskType.Synchronous<TChunkKey> sync =>  sync.ChunkTask(chunk, stage),
        _ => throw new NotSupportedException()
    };
}