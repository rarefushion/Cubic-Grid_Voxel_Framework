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
    public IEnumerable<Vector3D<int>> ChunksInPipeline => chunkByPos.Keys;
    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="P:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.chunksProcessingStage"]'/>
    public readonly List<Vector3D<int>>[] chunksProcessingStage;
    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="P:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.chunksEnqueuedStage"]'/>
    public readonly List<Vector3D<int>>[] chunksEnqueuedStage;
    /// <summary>Read-only view of chunks currently prioritized for a given stage.</summary>
    public readonly Queue<Vector3D<int>>[] priorityChunksStage;
    public IEnumerable<Vector3D<int>> ChunksPrioritized => priorityChunksStage.SelectMany(queue => queue);
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
        chunksEnqueuedStage[0].Add(pos);
    }

    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="M:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.DiscardChunk(Silk.NET.Maths.Vector3D{System.Int32})"]'/>
    public void DiscardChunk(Vector3D<int> pos)
    {
        if (!chunkByPos.ContainsKey(pos))
            return;
        chunkByPos.Remove(pos);
    }

    public void PrioritizeChunk(Vector3D<int> pos)
    {
        if (!chunkByPos.TryGetValue(pos, out ChunkGenData? chunkData))
            throw new ArgumentException($"Can't prioritize chunk at {pos} because it is not in the generation pipeline.");
        priorityChunksStage[chunkData.Stage].Enqueue(pos);
    }

    private bool UpdateChunkStateByStage(int stage)
    {
        for (int i = chunksProcessingStage[stage].Count - 1; i >= 0; i--)
        {
            Vector3D<int> chunk = chunksProcessingStage[stage][i];
            if (!chunkByPos.TryGetValue(chunk, out ChunkGenData? chunkData))
            {
                chunksProcessingStage[stage].RemoveAt(i);
                continue;
            }
            if (chunkData.Task == null)
                continue;
            if (!chunkData.Task!.IsCompleted)
                continue;
            // Drop and ignore chunks that were removed from chunkProcessingByPos.
            if (chunkData.Task!.IsCompletedSuccessfully && chunkByPos.ContainsKey(chunkData.Position))
            {
                ChunkGenData updatedData = chunkData with { Stage = chunkData.Stage + 1, Task = null };
                chunkByPos[chunkData.Position] = updatedData;
                StageCompleted?.Invoke(updatedData.Position, stage);
                if (updatedData.Stage <= maxStage)
                    chunksEnqueuedStage[updatedData.Stage].Add(chunk);
                else
                    chunkByPos.Remove(chunkData.Position);
            }
            chunksProcessingStage[stage].RemoveAt(i);
        }
        return true; // Usless return type. Only for pretty while loop
    }

    private bool QueueNextChunksByStage(int stage, CancelationCondition cancelationCondition)
    {
        int queueCount = priorityChunksStage[stage].Count;
        while(chunksEnqueuedStage[stage].Count > 0)
        {
            if (cancelationCondition())
                return false;
            if (!semaphore.Wait(0))
                return false;

            Vector3D<int> chunk;
            if (queueCount-- <= 0 || !priorityChunksStage[stage].TryDequeue(out chunk))
                chunk = chunksEnqueuedStage[stage][0];

            if (!processor.IsReadyForNextStage(chunk, stage))
            {
                semaphore.Release();
                continue;
            }
            chunksEnqueuedStage[stage].Remove(chunk);
            if (!chunkByPos.TryGetValue(chunk, out ChunkGenData? chunkData))
            {
                semaphore.Release();
                continue;
            }
            chunkData = chunkData with
            {
                Task = processor.ProcessStage(chunk, stage).ContinueWith(t =>
                {
                    semaphore.Release();
                    if (t.IsFaulted)
                    {
                        ErrorThrown?.Invoke($"Error processing chunk({chunk}) stage :{chunkData.Stage}. {t.Exception.InnerException?.Message}");
                        chunksFailed.Add(chunk);
                    }
                })
            };
            chunkByPos[chunk] = chunkData;
            chunksProcessingStage[stage].Add(chunk);
        }
        return true;
    }

    public ChunkClusterGenerationManager(IChunkProcessor chunkProcessor, int maxChunksProcessing)
    {
        processor = chunkProcessor;
        semaphore = new(maxChunksProcessing);
        maxStage = chunkProcessor.StagesCount - 1;
        chunksProcessingStage = new List<Vector3D<int>>[chunkProcessor.StagesCount];
        for (int i = 0; i < chunkProcessor.StagesCount; i++)
            chunksProcessingStage[i] = [];
        chunksEnqueuedStage = new List<Vector3D<int>>[chunkProcessor.StagesCount];
        for (int i = 0; i < chunkProcessor.StagesCount; i++)
            chunksEnqueuedStage[i] = [];
        priorityChunksStage = new Queue<Vector3D<int>>[chunkProcessor.StagesCount];
        for (int i = 0; i < chunkProcessor.StagesCount; i++)
            priorityChunksStage[i] = [];

    }
}