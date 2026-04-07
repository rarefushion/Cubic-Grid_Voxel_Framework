using System.Collections.Concurrent;
using Silk.NET.Maths;

namespace GalensUnified.CubicGrid.Framework;

/// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="T:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager"]'/>
public class ChunkClusterGenerationManager : IChunkClusterGenerationManager
{
    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="T:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.ChunkGenData"]'/>
    public record ChunkGenData(Vector3D<int> Position, int Stage, Task? Task);

    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="F:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.ErrorThrown"]'/>
    public Action<string>? ErrorThrown { get; set; }
    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="F:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.StageCompleted"]'/>
    public Action<Vector3D<int>, int>? StageCompleted { get; set; }
    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="F:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.chunksFailed"]'/>
    public readonly ConcurrentBag<Vector3D<int>> chunksFailed = [];
    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="F:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.chunkByPos"]'/>
    public readonly Dictionary<Vector3D<int>, ChunkGenData> chunkByPos = [];
    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="P:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.chunksProcessingStage"]'/>
    public readonly List<ChunkGenData>[] chunksProcessingStage;
    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="P:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.chunksEnqueuedStage"]'/>
    public readonly List<ChunkGenData>[] chunksEnqueuedStage;
    public readonly IChunkProcessor processor;

    private readonly int maxStage;

    private readonly SemaphoreSlim semaphore;

    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="M:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.ProcessChunks(GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.CancelationCondition)"]'/>
    public void ProcessChunks(CancelationCondition cancelationCondition)
    {
        int stage = 0;
        while(!cancelationCondition() && UpdateChunkStateByStage(stage++) && stage <= maxStage);
        stage--;
        while(!cancelationCondition() && QueueNextChunksByStage(stage--, cancelationCondition) && stage >= 0);
    }

    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="M:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.EnqueueChunk(Silk.NET.Maths.Vector3D{System.Int32})"]'/>
    public void EnqueueChunk(Vector3D<int> pos)
    {
        if (chunkByPos.ContainsKey(pos))
            return;
        ChunkGenData chunk = new(pos, 0, null);
        chunkByPos.Add(pos, chunk);
        chunksEnqueuedStage[0].Add(chunk);
    }

    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="M:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.DiscardChunk(Silk.NET.Maths.Vector3D{System.Int32})"]'/>
    public void DiscardChunk(Vector3D<int> pos)
    {
        if (!chunkByPos.ContainsKey(pos))
            return;
        chunkByPos.Remove(pos);
    }

    private bool UpdateChunkStateByStage(int stage)
    {
        List<ChunkGenData> chunks = chunksProcessingStage[stage];
        for (int i = chunks.Count - 1; i >= 0; i--)
        {
            if (chunks[i].Task == null)
                continue;
            if (!chunks[i].Task!.IsCompleted)
                continue;
            // Drop and ignore chunks that were removed from chunkProcessingByPos.
            if (chunks[i].Task!.IsCompletedSuccessfully && chunkByPos.ContainsKey(chunks[i].Position))
            {
                ChunkGenData updatedData = chunks[i] with { Stage = chunks[i].Stage + 1, Task = null };
                chunkByPos[chunks[i].Position] = updatedData;
                StageCompleted?.Invoke(updatedData.Position, stage);
                if (updatedData.Stage <= maxStage)
                    chunksEnqueuedStage[updatedData.Stage].Add(updatedData);
                else
                    chunkByPos.Remove(chunks[i].Position);
            }
            chunks.RemoveAt(i);
        }
        return true; // Usless return type. Only for pretty while loop
    }

    private bool QueueNextChunksByStage(int stage, CancelationCondition cancelationCondition)
    {
        for (int i = chunksEnqueuedStage[stage].Count - 1; i >= 0; i --)
        {
            if (cancelationCondition())
                return false;
            if (!semaphore.Wait(0))
                return false;

            if (chunksEnqueuedStage[stage].Count == 0)
            {
                semaphore.Release();
                return true;
            }

            ChunkGenData chunk = chunksEnqueuedStage[stage][0];
            if (!processor.IsReadyForNextStage(chunk.Position, stage))
            {
                semaphore.Release();
                continue;
            }
            chunksEnqueuedStage[stage].RemoveAt(0);
            if (!chunkByPos.ContainsKey(chunk.Position))
            {
                semaphore.Release();
                continue;
            }
            chunk = chunk with
            {
                Task = processor.ProcessStage(chunk.Position, stage).ContinueWith(t =>
                {
                    semaphore.Release();
                    if (t.IsFaulted)
                    {
                        ErrorThrown?.Invoke($"Error processing chunk({chunk.Position}) stage :{chunk.Stage}. {t.Exception.InnerException?.Message}");
                        chunksFailed.Add(chunk.Position);
                    }
                })
            };
            chunksProcessingStage[stage].Add(chunk);
        }
        return true;
    }

    public ChunkClusterGenerationManager(IChunkProcessor chunkProcessor, int maxChunksProcessing)
    {
        processor = chunkProcessor;
        semaphore = new(maxChunksProcessing);
        maxStage = chunkProcessor.StagesCount - 1;
        chunksProcessingStage = new List<ChunkGenData>[chunkProcessor.StagesCount];
        for (int i = 0; i < chunkProcessor.StagesCount; i++)
            chunksProcessingStage[i] = [];
        chunksEnqueuedStage = new List<ChunkGenData>[chunkProcessor.StagesCount];
        for (int i = 0; i < chunkProcessor.StagesCount; i++)
            chunksEnqueuedStage[i] = [];

    }
}