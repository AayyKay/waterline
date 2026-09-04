using System.Text.Json.Serialization;

namespace Waterline;

public sealed class DrinkEntry
{
    public long Id { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public double AmountOz { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.Now;
    [JsonIgnore] public string TimeLabel => At.LocalDateTime.ToString("h:mm tt");
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

public sealed class WaterlineState
{
    public WaterlineSettings Settings { get; set; } = new();
    public List<DrinkEntry> Drinks { get; set; } = [];
    public DateTimeOffset? LastNotificationAt { get; set; }
}

public readonly record struct ReminderPlan(DateTimeOffset? DueAt, bool IsActive);
public readonly record struct AppNotification(string Title, string Message);
public readonly record struct DailyTotal(string Label, double TotalOz, double BarHeight);
