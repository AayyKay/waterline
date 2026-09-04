namespace Waterline;

public static class ReminderScheduler
{
    public static ReminderPlan GetPlan(
        DateTimeOffset now,
        WaterlineSettings settings,
        double totalOz,
        DateTimeOffset? lastDrinkAt,
        DateTimeOffset? lastNotificationAt)
    {
        if (!settings.RemindersEnabled || totalOz >= settings.DailyGoalOz || settings.ReminderDays.Count == 0)
            return new ReminderPlan(null, false);

        var localNow = now.LocalDateTime;
        var start = new DateTimeOffset(localNow.Date + settings.WorkdayStart, now.Offset);
        var end = new DateTimeOffset(localNow.Date + settings.WorkdayEnd, now.Offset);
        var enabledToday = settings.ReminderDays.Contains(localNow.DayOfWeek);
        var active = enabledToday && now >= start && now <= end;
        var interval = TimeSpan.FromMinutes(settings.ReminderIntervalMinutes);

        if (!enabledToday || now > end)
        {
            for (var offset = 1; offset <= 7; offset++)
            {
                var day = localNow.Date.AddDays(offset);
                if (settings.ReminderDays.Contains(day.DayOfWeek))
                    return new ReminderPlan(new DateTimeOffset(day + settings.WorkdayStart, now.Offset) + interval, false);
            }
            return new ReminderPlan(null, false);
        }

        if (now < start) return new ReminderPlan(start + interval, false);

        var anchor = start;
        if (lastDrinkAt is { } drink && drink > anchor) anchor = drink;
        if (lastNotificationAt is { } notification && notification > anchor) anchor = notification;
        var dueAt = anchor + interval;
        return new ReminderPlan(dueAt <= end ? dueAt : null, active);
    }
}
