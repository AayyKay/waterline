namespace Waterline.Core;

public static class HydrationCalculator
{
    public static HydrationProgress GetProgress(
        IEnumerable<DrinkEntry> entries,
        DateOnly localDay,
        double goalOz,
        TimeZoneInfo timeZone)
    {
        var total = entries
            .Where(entry => GetLocalDay(entry.RecordedAt, timeZone) == localDay)
            .Sum(entry => entry.AmountOz);
        var safeGoal = double.IsFinite(goalOz) && goalOz > 0 ? goalOz : 0;
        var remaining = Math.Max(0, safeGoal - total);
        var percent = safeGoal == 0 ? 0 : Math.Max(0, total / safeGoal * 100);
        return new HydrationProgress(total, safeGoal, remaining, percent, safeGoal > 0 && total >= safeGoal);
    }

    public static IReadOnlyList<DailyHydration> GetRecentDays(
        IEnumerable<DrinkEntry> entries,
        DateOnly throughDay,
        int dayCount,
        double goalOz,
        TimeZoneInfo timeZone)
    {
        if (dayCount <= 0) return [];
        var totals = entries
            .GroupBy(entry => GetLocalDay(entry.RecordedAt, timeZone))
            .ToDictionary(group => group.Key, group => group.Sum(entry => entry.AmountOz));

        return Enumerable.Range(0, dayCount)
            .Select(offset => throughDay.AddDays(offset - dayCount + 1))
            .Select(day =>
            {
                var total = totals.GetValueOrDefault(day);
                return new DailyHydration(day, total, goalOz > 0 && total >= goalOz);
            })
            .ToList();
    }

    public static IReadOnlyList<DrinkEntry> GetEntriesForDay(
        IEnumerable<DrinkEntry> entries,
        DateOnly localDay,
        TimeZoneInfo timeZone) => entries
        .Where(entry => GetLocalDay(entry.RecordedAt, timeZone) == localDay)
        .OrderByDescending(entry => entry.RecordedAt)
        .ToList();

    public static DrinkEntry? GetMostRecentEntry(
        IEnumerable<DrinkEntry> entries,
        DateOnly localDay,
        TimeZoneInfo timeZone) => GetEntriesForDay(entries, localDay, timeZone).FirstOrDefault();

    public static DateOnly GetLocalDay(DateTimeOffset instant, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, timeZone).DateTime);
}
