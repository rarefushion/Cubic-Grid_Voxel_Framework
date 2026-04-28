namespace GalensUnified.CubicGrid.Framework;

public class ChunkGenerationPipeline<TChunkKey>(IChunkProcessor<TChunkKey> processor) :
    IChunkGenerationPipeline<TChunkKey>
    where TChunkKey : notnull, IEquatable<TChunkKey>
{
    public int StagesCount => processor.StagesCount;
    public IEnumerable<ChunkGenerating<TChunkKey>> ChunksInPipeline => chunkByPos.Values;
    private readonly IChunkProcessor<TChunkKey> processor = processor;
    private readonly Dictionary<TChunkKey, ChunkGenerating<TChunkKey>> chunkByPos = [];

    public void StartChunk(TChunkKey toStart) =>
        chunkByPos.Add(toStart, new ChunkGenerating<TChunkKey>(toStart, 0, StartChunkTask(toStart, 0)));

    public IEnumerable<ChunkGenerating<TChunkKey>> ProcessChunks()
    {
        ChunkGenerating<TChunkKey>[] chunks = [.. chunkByPos.Values];
        foreach (ChunkGenerating<TChunkKey> chunk in chunks)
            if (chunk.Task.IsCompleted)
            {
                if (chunk.Task.IsCompletedSuccessfully)
                {
                    int nextStage = chunk.Stage + 1;
                    if (nextStage >= StagesCount)
                        chunkByPos.Remove(chunk.Chunk);
                    else
                        chunkByPos[chunk.Chunk] = chunk with
                        {
                            Stage = nextStage,
                            Task = StartChunkTask(chunk.Chunk, nextStage)
                        };
                    yield return chunk;
                }
                else
                    throw new Exception($"Chunk {chunk.Chunk} failed to process stage {chunk.Stage}.", chunk.Task.Exception);
            }
    }

    public Task StartChunkTask(TChunkKey chunk, int stage) => processor.GetChunkTask(chunk, stage) switch
    {
        ChunkTaskType.Async<TChunkKey> async =>  Task.Run(() => async.ChunkTask(chunk, stage)),
        ChunkTaskType.Synchronous<TChunkKey> sync =>  sync.ChunkTask(chunk, stage),
        _ => throw new NotSupportedException()
    };
}