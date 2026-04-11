namespace GalensUnified.CubicGrid.Framework;

public interface IChunkProcessor<TChunkKey>
{
    /// <summary>Number of stages.</summary>
    int StagesCount { get; }
    /// <summary>False if this chunk needs to wait. True to start processing.</summary>
    bool IsReadyForNextStage(TChunkKey chunk, int stage);
    /// <summary>Request the task for processing. Can be async or simply return Task.CompletedTask when done.</summary>
    Task ProcessStage(TChunkKey chunk, int stage);
}