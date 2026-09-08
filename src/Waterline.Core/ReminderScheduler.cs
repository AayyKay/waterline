namespace Waterline.Core;

public static class ReminderScheduler
{
    public static ReminderPlan GetPlan(
        DateTimeOffset now,
        WaterlineSettings settings,
        double totalOz,
        DateTimeOffset? lastDrinkAt,
        DateTimeOffset? lastNotificationAt,
        TimeZoneInfo? timeZone = null)
    {
        if (!settings.RemindersEnabled ||
            totalOz >= settings.DailyGoalOz ||
            settings.ReminderDays.Count == 0 ||
            settings.ReminderIntervalMinutes <= 0 ||
            settings.WorkdayStart >= settings.WorkdayEnd)
            return new ReminderPlan(null, false);

        var zone = timeZone ?? TimeZoneInfo.Local;
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var localDate = DateOnly.FromDateTime(localNow.DateTime);
        var start = AtLocalTime(localDate, settings.WorkdayStart, zone);
        var end = AtLocalTime(localDate, settings.WorkdayEnd, zone);
        var enabledToday = settings.ReminderDays.Contains(localNow.DayOfWeek);
        var active = enabledToday && now >= start && now <= end;
        var interval = TimeSpan.FromMinutes(settings.ReminderIntervalMinutes);

        if (!enabledToday || now > end)
        {
            for (var offset = 1; offset <= 7; offset++)
            {
                var day = localDate.AddDays(offset);
                if (settings.ReminderDays.Contains(day.DayOfWeek))
                    return new ReminderPlan(AtLocalTime(day, settings.WorkdayStart, zone) + interval, false);
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

    private static DateTimeOffset AtLocalTime(DateOnly day, TimeSpan time, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue).Add(time);
        if (zone.IsInvalidTime(local)) local = local.AddHours(1);
        var offset = zone.IsAmbiguousTime(local)
            ? zone.GetAmbiguousTimeOffsets(local).Max()
            : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }
}
