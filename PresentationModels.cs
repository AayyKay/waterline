namespace Waterline;

public readonly record struct AppNotification(string Title, string Message);
public readonly record struct DailyTotal(string Label, double TotalOz, double BarHeight);
