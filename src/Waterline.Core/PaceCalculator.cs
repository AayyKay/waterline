namespace Waterline.Core;

public static class PaceCalculator
{
    public static PaceResult Calculate(
        DateTimeOffset now,
        WaterlineSettings settings,
        double totalOz,
        TimeZoneInfo timeZone)
    {
        var remaining = Math.Max(0, settings.DailyGoalOz - totalOz);
        if (remaining <= 0)
            return new PaceResult(PaceStatus.Complete, totalOz, settings.DailyGoalOz, totalOz - settings.DailyGoalOz, 0, 0);

        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        if (!settings.ReminderDays.Contains(localNow.DayOfWeek))
            return new PaceResult(PaceStatus.NoPlan, totalOz, 0, totalOz, remaining, 0);

        var start = localNow.Date + settings.WorkdayStart;
        var end = localNow.Date + settings.WorkdayEnd;
        if (localNow.DateTime < start)
            return new PaceResult(PaceStatus.BeforeSchedule, totalOz, 0, totalOz, remaining, 0);
        if (localNow.DateTime > end)
            return new PaceResult(PaceStatus.AfterSchedule, totalOz, settings.DailyGoalOz, totalOz - settings.DailyGoalOz, remaining, 0);

        var elapsed = (localNow.DateTime - start).TotalMinutes;
        var duration = Math.Max(1, (end - start).TotalMinutes);
        var target = settings.DailyGoalOz * Math.Clamp(elapsed / duration, 0, 1);
        var difference = totalOz - target;
        var tolerance = Math.Max(4, settings.DailyGoalOz * .05);
        var status = difference < -tolerance
            ? PaceStatus.Behind
            : difference > tolerance
                ? PaceStatus.Ahead
                : PaceStatus.OnPace;
        var suggested = status == PaceStatus.Behind
            ? Math.Min(remaining, Math.Min(16, Math.Max(8, Math.Ceiling(Math.Abs(difference) / 4) * 4)))
            : 0;

        return new PaceResult(status, totalOz, target, difference, remaining, suggested);
    }
}
