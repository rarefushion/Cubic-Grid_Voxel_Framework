namespace GalensUnified.Debugging.ImGui;
using ImGuiNET;

/// <summary>
/// A collection of delta times, e.g frame times.
/// With integration an simple construction of ImGui tables.
/// </summary>
/// <remarks>
/// A standard usage for frame rate over the last 5 seconds might look like:
/// new DeltaLogs("5S FPS", new TimeSpan(0, 0, 5)) >
/// every frame call <see cref="LogDelta(double)"/> with time since last frame >
/// creating a list of <see cref="IDefaultTimeTableRow"/> constructors >
/// passing them into <see cref="DrawImGuiTimeTable(List{IDefaultTimeTableRow})"/> >
/// finally ensure you have a ImGuiController and you're calling Render on it.
/// </remarks>
public class DeltaLogs
{
    /// <summary>A log of the time it took for something to occur.</summary>
    /// <param name="Recorded">Date this log was recorded.</param>
    /// <param name="Delta">Time you recorded for this entry.</param>
    public record DeltaLog(DateTime Recorded, double Delta);

    /// <summary>A row to be used in the time table.</summary>
    /// <param name="Name">Name of the row to identify it.</param>
    /// <param name="Time">
    /// The time data of this row.
    /// Often the average of some deltas.
    /// Will also be displayed as FPS (1/time).
    /// It is assumed you called LogDelta in ms.
    /// </param>
    public record TimeTableRow(string Name, double Time);
    public interface IDefaultTimeTableRow { string Name { get; } }
    /// <summary>Creates a row using the average of every entry logged.</summary>
    public record TotalAverageTimeTableRow(string Name) : IDefaultTimeTableRow;
    /// <summary>Creates a row using a percentage of the longest logged <see cref="DeltaLog.Delta"/>s.</summary>
    /// <param name="Percentile">Percentage of the logs to use. Value must be between 0-100.</param>
    public record TopPercentTimeTableRow(string Name, float Percentile) : IDefaultTimeTableRow;
    /// <summary>Creates a row using a number of the longest logged <see cref="DeltaLog.Delta"/>s.</summary>
    public record TopCountTimeTableRow(string Name, int Count) : IDefaultTimeTableRow;

    public string tableName;

    public DeltaLog[] Logs => [.. logs];

    private int? maxCount;
    private TimeSpan? maxTime;

    private readonly Queue<DeltaLog> logs;

    public void LogDelta(double delta)
    {
        if (maxCount != null)
            while (logs.Count >= maxCount)
                logs.Dequeue();

        logs.Enqueue(new DeltaLog(DateTime.Now, delta));

        if (maxTime != null)
            while (logs.Count > 0 && DateTime.Now - logs.Peek().Recorded > maxTime)
                logs.Dequeue();
    }

    public void ClearLogs()
    {
        logs.Clear();
        logs.TrimExcess(maxCount ?? 0);
    }

    /// <summary>Sets the max number of logs and removes excess.</summary>
    public void SetMaxLogsCount(int? maxCount)
    {
        this.maxCount = maxCount;
        if (maxCount != null)
        {
            while (logs.Count > maxCount)
                logs.Dequeue();
            logs.TrimExcess(maxCount.Value);
        }
    }

    /// <summary>
    /// Sets the time a log can exist and removes logs exceeding it.
    /// Logs are only checked when a new one is logged.
    /// Refers to <see cref="DeltaLog.Recorded"/> not Delta.
    /// </summary>
    public void SetMaxRecordTime(TimeSpan maxRecordTime)
    {
        this.maxTime = maxRecordTime;

        if (maxTime != null)
        {
            while (logs.Count > 0 && DateTime.Now - logs.Peek().Recorded > maxRecordTime)
                logs.Dequeue();
            logs.TrimExcess();
        }
    }

    public void ClearMaxTimeAndCount()
    {
        maxCount = null;
        maxTime = null;
    }

    public List<TimeTableRow> GenerateDefaultRows(List<IDefaultTimeTableRow> defaultConstructors) =>
        [.. defaultConstructors.Select(constructor => constructor switch 
        {
            TotalAverageTimeTableRow row => new TimeTableRow(row.Name, GetAverage()),
            TopPercentTimeTableRow row => new TimeTableRow(row.Name, GetAverageOfPercentile(row.Percentile)),
            TopCountTimeTableRow row => new TimeTableRow(row.Name, GetAverageOfCount(row.Count)),
            _ => throw new Exception()
        })];

    public void DrawImGuiTimeTable(List<IDefaultTimeTableRow> defaultConstructors) =>
        DrawImGuiTimeTable(GenerateDefaultRows(defaultConstructors));

    public void DrawImGuiTimeTable(List<TimeTableRow> rows, List<IDefaultTimeTableRow> defaultConstructors) =>
        DrawImGuiTimeTable([.. rows.Concat(GenerateDefaultRows(defaultConstructors))]);

    /// <summary>
    /// Draws a table with rows of Name, Time and FPS.
    /// Rows that are most likely made with the various GetAverage functions or the default constructors.
    /// Time is assumed to be in ms.
    /// </summary>
    public void DrawImGuiTimeTable(List<TimeTableRow> rows)
    {
        if (!ImGui.CollapsingHeader($"{tableName} Time Table", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        if (ImGui.BeginTable($"{tableName} Time Table", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Name");
            ImGui.TableSetupColumn("Time");
            ImGui.TableSetupColumn("FPS");
            ImGui.TableHeadersRow();

            static void AddRow(string label, double value)
            {
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted(label);

                ImGui.TableSetColumnIndex(1);
                ImGui.TextUnformatted($"{value:N3}");

                ImGui.TableSetColumnIndex(2);
                ImGui.TextUnformatted(((int)Math.Floor(1d / value)).ToString());
            }

            foreach (TimeTableRow row in rows)
                AddRow(row.Name, row.Time);

            ImGui.EndTable();
        }
    }

    /// <summary>Gets the total average from all logged deltas.</summary>
    public double GetAverage() => 
        logs.Any() ? logs.Average(x => x.Delta) : 0;

    /// <summary>Gets the average from a percentage of the longest logged deltas.</summary>
    /// <param name="percentile">Percentage of the logs to use. Value must be between 0-100.</param>
    public double GetAverageOfPercentile(float percentile) =>
        GetAverageOfCount((int)Math.Ceiling(logs.Count * (percentile / 100.0)));


    /// <summary>Gets the average from the longest logged deltas.</summary>
    public double GetAverageOfCount(int count) => logs
        .OrderByDescending(x => x.Delta)
        .Take(count)
        .OrderBy(x => x.Recorded)
        .Average(x => x.Delta);

    /// <param name="name">The name used for the ImGUI table.</param>
    public DeltaLogs(string name)
    {
        tableName = name;
        maxCount = null;
        maxTime = null;
        logs = [];
    }

    /// <param name="name">The name used for the ImGUI table.</param>
    /// <param name="maxRecordTime">
    /// Max amount of time a logged entry can exist.
    /// Logs are only checked when a new one is logged.
    /// Refers to <see cref="DeltaLog.Recorded"/> not Delta.
    /// </param>
    public DeltaLogs(string name, TimeSpan maxRecordTime)
    {
        tableName = name;
        maxCount = null;
        maxTime = maxRecordTime;
        logs = [];
    }

    /// <param name="name">The name used for the ImGUI table.</param>
    /// <param name="maxLogs">Max number of DeltaLogs to store.</param>
    public DeltaLogs(string name, int maxLogs)
    {
        tableName = name;
        maxCount = maxLogs;
        maxTime = null;
        logs = new(maxLogs);
    }

    /// <param name="name">The name used for the ImGUI table.</param>
    /// <param name="maxLogs">Max number of DeltaLogs to store.</param>
    /// <param name="maxRecordTime">
    /// Max amount of time a logged entry can exist.
    /// Logs are only checked when a new one is logged.
    /// Refers to <see cref="DeltaLog.Recorded"/> not Delta.
    /// </param>
    public DeltaLogs(string name, int maxLogs, TimeSpan maxRecordTime)
    {
        tableName = name;
        maxCount = maxLogs;
        maxTime = maxRecordTime;
        logs = new(maxLogs);
    }
}