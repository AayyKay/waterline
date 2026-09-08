using System.Text.Json.Serialization;

namespace Waterline.Core;

public static class StateSchema
{
    public const int CurrentVersion = 1;
}

public sealed class DrinkEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public double AmountOz { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.Now;

    [JsonIgnore] public string TimeLabel => RecordedAt.LocalDateTime.ToString("h:mm tt");
    [JsonIgnore] public string AmountLabel => $"{AmountOz:0.#} oz";
}

public sealed class WaterlineSettings
{
    public double DailyGoalOz { get; set; } = 80;
    public int ReminderIntervalMinutes { get; set; } = 60;
    public TimeSpan WorkdayStart { get; set; } = new(9, 0, 0);
    public TimeSpan WorkdayEnd { get; set; } = new(17, 0, 0);
    public bool RemindersEnabled { get; set; }
    public bool SoundsEnabled { get; set; } = true;
    public HashSet<DayOfWeek> ReminderDays { get; set; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
}

public sealed class DesktopState
{
    public string WidgetMode { get; set; } = "expanded";
    public WidgetPlacement? WidgetPlacement { get; set; }
}

public sealed class WidgetPlacement
{
    public string? MonitorId { get; set; }
    public double AnchorX { get; set; } = 1;
    public double AnchorY { get; set; } = 1;
    public double Width { get; set; } = 400;
    public double Height { get; set; } = 500;
}

public sealed class RuntimeState
{
    public DateTimeOffset? LastNotificationAt { get; set; }
}

public sealed class WaterlineState
{
    public int SchemaVersion { get; set; } = StateSchema.CurrentVersion;
    public WaterlineSettings Settings { get; set; } = new();
    public List<DrinkEntry> Drinks { get; set; } = [];
    public DesktopState Desktop { get; set; } = new();
    public RuntimeState Runtime { get; set; } = new();
}

public readonly record struct ReminderPlan(DateTimeOffset? DueAt, bool IsActive);

public enum PaceStatus
{
    NoPlan,
    BeforeSchedule,
    OnPace,
    Behind,
    Ahead,
    Complete,
    AfterSchedule
}

public readonly record struct HydrationProgress(
    double TotalOz,
    double GoalOz,
    double RemainingOz,
    double Percent,
    bool IsComplete);

public readonly record struct DailyHydration(DateOnly Day, double TotalOz, bool GoalReached);

public readonly record struct PaceResult(
    PaceStatus Status,
    double TotalOz,
    double TargetOz,
    double DifferenceOz,
    double RemainingOz,
    double SuggestedNextOz);
