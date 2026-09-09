namespace Waterline;

public readonly record struct AppNotification(string Title, string Message);
public readonly record struct DailyTotal(string Label, double TotalOz, double BarHeight);
public sealed record DrinkEntryView(string Id, string AmountLabel, string TimeLabel, string AccessibleLabel);

public sealed record HistoryDayView(
    DateOnly Day,
    string DayName,
    string DateLabel,
    string TotalLabel,
    double ProgressPercent,
    bool GoalReached,
    bool IsOverGoal,
    string StatusLabel);
