using ImGuiNET;
using GalensUnified.Debugging.ImGui;

public static class DebugRenderer
{
    public const bool showDefaultFPS = true;
    public static bool showDebugInfo = true;
    public static bool showDeltaLogs = true;


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

    public static void Load()
    {
        if (showDefaultFPS)
            foreach (DeltaLogs table in defaultFPSTables)
                AddDeltaTable(table, defaultTimeTableRows);
    }

    public static void OnRender(double delta)
    {
        if (showDefaultFPS)
            foreach (DeltaLogs table in defaultFPSTables)
                frameTimesByTableName[table.tableName].LogDelta(delta);

        if (!showDebugInfo)
            return;

        ImGui.Begin("Debug");
        if (showDeltaLogs)
        {
            foreach (DeltaLogs logs in frameTimesByTableName.Values)
                logs.DrawImGuiTimeTable(defaultTimeTableRows);
        }
        ImGui.End();
    }
}