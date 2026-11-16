using ImGuiNET;
using GalensUnified.Debugging.ImGui;
using GalensUnified.CubicGrid.Core;
using System.Numerics;
using Silk.NET.Maths;

public static class DebugRenderer
{
    public const bool showDefaultFPS = true;
    public static bool showDebugInfo = true;
    public static bool showDeltaLogs = true;

    private static int chunkLength;


    // These two defaults can be modified directly.
    static readonly DeltaLogs[] defaultFPSTables =
        [
            new("1S FPS", new TimeSpan(0, 0, 1)),
            new("5S FPS", new TimeSpan(0, 0, 5)),
            new("15S FPS", new TimeSpan(0, 0, 15)),
        ];
    public static readonly List<DeltaLogs.IDefaultTimeTableRow> defaultTimeTableRows =
        [
            new DeltaLogs.TotalAverageTimeTableRow("AVG"),
            new DeltaLogs.TopPercentTimeTableRow("1.0%", 1),
            new DeltaLogs.TopPercentTimeTableRow("0.1%", 0.1f),
            new DeltaLogs.TopCountTimeTableRow("Max", 1)
        ];
    static readonly Dictionary<string, DeltaLogs> frameTimesByTableName = [];
    static readonly Dictionary<string, List<DeltaLogs.IDefaultTimeTableRow>> rowConstructorsByTableName = [];

    public static void AddDeltaTable(DeltaLogs table, List<DeltaLogs.IDefaultTimeTableRow> defaultConstructors)
    {
        frameTimesByTableName.Add(table.tableName, table);
        rowConstructorsByTableName.Add(table.tableName, defaultConstructors);
    }

    public static void Load(int chunkLength)
    {
        if (showDefaultFPS)
            foreach (DeltaLogs table in defaultFPSTables)
                AddDeltaTable(table, defaultTimeTableRows);
        DebugRenderer.chunkLength = chunkLength;
    }

    public static void OnRender(double delta, int chunksGenerating)
    {
        if (showDefaultFPS)
            foreach (DeltaLogs table in defaultFPSTables)
                frameTimesByTableName[table.tableName].LogDelta(delta);

        if (!showDebugInfo)
            return;

        Vector3 chunkPos = Program.playerCam.Position / chunkLength;
        Vector3D<int> chunkCoords = chunkPos.Floor();
        Vector3D<int> playerBlockInChunk = ((chunkPos - (Vector3)chunkCoords) * chunkLength).Floor();
        ImGui.Begin("Debug");
        ImGui.Text($"Position: {Program.playerCam.Position:N1}");
        ImGui.Text
        (
            $"Position Chunk: " +
            $"{chunkCoords.X:N0}.{playerBlockInChunk.X:N0}, " +
            $"{chunkCoords.Y:N0}.{playerBlockInChunk.Y:N0}, " +
            $"{chunkCoords.Z:N0}.{playerBlockInChunk.Z:N0}"
        );
        ImGui.Text($"Speed: {Program.moveSpeed:N1}/s");
        ImGui.Text($"Generating {chunksGenerating} chunks.");
        if (showDeltaLogs)
        {
            foreach (DeltaLogs logs in frameTimesByTableName.Values)
                logs.DrawImGuiTimeTable(defaultTimeTableRows);
        }
        ImGui.End();
    }
}