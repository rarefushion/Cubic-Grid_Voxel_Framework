using System.Collections.Concurrent;
using Silk.NET.Maths;

namespace GalensUnified.CubicGrid.Framework;

// Should be replaced
public delegate bool NeighborPointsCalculated(Vector3D<int> innerChunk);

/// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="T:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager"]'/>
public class ChunkClusterGenerationManager(IChunkProcessor chunkProcessor, int maxChunksProcessing, NeighborPointsCalculated neighborTest)
{
    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="F:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.ErrorThrown"]'/>
    public Action<string>? ErrorThrown;
    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="F:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.chunksFailed"]'/>
    public readonly ConcurrentBag<Vector3D<int>> chunksFailed = [];
    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="F:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.chunkProcessingByPos"]'/>
    public readonly Dictionary<Vector3D<int>, ChunkGenerationHandler> chunkProcessingByPos = [];
    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="T:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.CancelationCondition"]'/>
    public delegate bool CancelationCondition();
    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="T:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.ChunkTask"]'/>
    public record ChunkTask(Task Task, Vector3D<int> Chunk);
    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="P:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.ChunksCalculatingPoints"]'/>
    public List<ChunkTask> ChunksCalculatingPoints => chunksCalculatingPoints;
    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="P:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.ChunksCalculatingNeighborPoints"]'/>
    public List<ChunkTask> ChunksCalculatingNeighborPoints => chunksCalculatingNeighborPoints;
    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="P:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.ChunksRendering"]'/>
    public List<ChunkTask> ChunksRendering => chunksRendering;
    private readonly NeighborPointsCalculated neighborTest = neighborTest;


    private readonly List<Vector3D<int>> chunksToCalculatePoints = [];
    private readonly List<Vector3D<int>> chunksAwaitingNeighbors = [];
    private readonly List<Vector3D<int>> chunksToRender = [];
    private readonly List<ChunkTask> chunksCalculatingPoints = [];
    private readonly List<ChunkTask> chunksCalculatingNeighborPoints = [];
    private readonly List<ChunkTask> chunksRendering = [];
    private readonly IChunkProcessor processor = chunkProcessor;
    private readonly SemaphoreSlim semaphore = new(maxChunksProcessing);

    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="M:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.ProcessChunks(GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.CancelationCondition)"]'/>
    public void ProcessChunks(CancelationCondition cancelationCondition)
    {
        if (cancelationCondition())
            return;

        UpdateChunkStateByStage(ChunkGenerationStage.Instantiated);
        UpdateChunkStateByStage(ChunkGenerationStage.PointsCalculated);
        UpdateChunkStateByStage(ChunkGenerationStage.ChunkNeighborsPointsCalculated);

        if (cancelationCondition())
            return;

        while(!cancelationCondition() && QueueNextChunkByStage(ChunkGenerationStage.ChunkNeighborsPointsCalculated));
        while(!cancelationCondition() && QueueNextChunkByStage(ChunkGenerationStage.PointsCalculated));
        while(!cancelationCondition() && QueueNextChunkByStage(ChunkGenerationStage.Instantiated));
    }

    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="M:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.OnChunkAdded(Silk.NET.Maths.Vector3D{System.Int32})"]'/>
    public void OnChunkAdded(Vector3D<int> pos)
    {
        if (chunkProcessingByPos.ContainsKey(pos))
            return;
        chunkProcessingByPos.Add(pos, new ChunkGenerationHandler(pos));
        chunksToCalculatePoints.Add(pos);
    }

    /// <include file='ChunkClusterGenerationManager.xml' path='doc/members/member[@name="M:GalensUnified.CubicGrid.Framework.ChunkClusterGenerationManager.OnChunkRemoved(Silk.NET.Maths.Vector3D{System.Int32})"]'/>
    public void OnChunkRemoved(Vector3D<int> pos)
    {
        if (!chunkProcessingByPos.ContainsKey(pos))
            return;
        chunkProcessingByPos.Remove(pos);
    }

    private void UpdateChunkStateByStage(ChunkGenerationStage stage)
    {
        List<ChunkTask> tasks = stage switch
        {
            ChunkGenerationStage.Instantiated => chunksCalculatingPoints,
            ChunkGenerationStage.PointsCalculated => chunksCalculatingNeighborPoints,
            ChunkGenerationStage.ChunkNeighborsPointsCalculated => chunksRendering,
            _ => throw new Exception("Stage not processable.")
        };
        for (int i = tasks.Count - 1; i >= 0; i--)
        {
            if (!tasks[i].Task.IsCompleted)
                continue;
            // Drop and ignore chunks that were removed from chunkProcessingByPos.
            if (tasks[i].Task.IsCompletedSuccessfully && chunkProcessingByPos.ContainsKey(tasks[i].Chunk))
            {
                ChunkGenerationStage completedStage = NextStage(stage);
                chunkProcessingByPos[tasks[i].Chunk].StageCompleted(completedStage);
                List<Vector3D<int>>? nextQueue = GetQueueForStage(completedStage);
                if (nextQueue != null)
                    nextQueue.Add(tasks[i].Chunk);
                else
                    chunkProcessingByPos.Remove(tasks[i].Chunk);
            }
            tasks.RemoveAt(i);
        }
    }

    private static ChunkGenerationStage NextStage(ChunkGenerationStage stage) => stage switch
    {
        ChunkGenerationStage.Instantiated => ChunkGenerationStage.PointsCalculated,
        ChunkGenerationStage.PointsCalculated => ChunkGenerationStage.ChunkNeighborsPointsCalculated,
        ChunkGenerationStage.ChunkNeighborsPointsCalculated => ChunkGenerationStage.Rendered,
        _ => throw new Exception("Stage not processable.")
    };

    private List<Vector3D<int>>? GetQueueForStage(ChunkGenerationStage stage) => stage switch
    {
        ChunkGenerationStage.PointsCalculated => chunksAwaitingNeighbors,
        ChunkGenerationStage.ChunkNeighborsPointsCalculated => chunksToRender,
        _ => null // Rendered or unknown = no next queue
    };

    delegate Task ProcessorType(Vector3D<int> chunk);

    private bool QueueNextChunkByStage(ChunkGenerationStage stage)
    {
        if (!semaphore.Wait(0))
            return false;

        ProcessorType processorType;
        List<Vector3D<int>> toProcess;
        List<ChunkTask> toCollect;
        bool needsNeighborTest = false;
        switch(stage)
        {
            case ChunkGenerationStage.Instantiated:
                processorType = processor.CalculatePointsAsync;
                toProcess = chunksToCalculatePoints;
                toCollect = chunksCalculatingPoints;
                break;
            case ChunkGenerationStage.PointsCalculated:
                processorType = processor.OnNeighborPointsCalculatedAsync;
                toProcess = chunksAwaitingNeighbors;
                toCollect = chunksCalculatingNeighborPoints;
                needsNeighborTest = true;
                break;
            case ChunkGenerationStage.ChunkNeighborsPointsCalculated:
                processorType = processor.RenderAsync;
                toProcess = chunksToRender;
                toCollect = chunksRendering;
                break;
            default:
                throw new Exception("Stage not processable.");
        }

        if (toProcess.Count == 0)
        {
            semaphore.Release();
            return false;
        }

        Vector3D<int> pos = toProcess[0];
        if (needsNeighborTest && !neighborTest(pos))
        {
            semaphore.Release();
            return false;
        }
        toProcess.Remove(pos);
        toCollect.Add(new(processorType(pos).ContinueWith(t =>
        {
            semaphore.Release();
            if (t.IsFaulted)
            {
                ErrorThrown?.Invoke($"Error calculating chunk({pos}). {t.Exception.InnerException?.Message}");
                chunksFailed.Add(pos);
            }
        }), pos));
        return true;
    }
}