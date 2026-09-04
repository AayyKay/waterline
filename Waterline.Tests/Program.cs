using Waterline;

var settings = new WaterlineSettings
{
    DailyGoalOz = 80,
    ReminderIntervalMinutes = 60,
    WorkdayStart = new TimeSpan(9, 0, 0),
    WorkdayEnd = new TimeSpan(17, 0, 0),
    RemindersEnabled = true
};

var failures = new List<string>();
Check("schedules from workday start", () =>
{
    var plan = ReminderScheduler.GetPlan(Local(2026, 9, 2, 8, 30), settings, 0, null, null);
    return plan.DueAt?.Hour == 10 && !plan.IsActive;
});
Check("logging a drink resets the interval", () =>
{
    var plan = ReminderScheduler.GetPlan(Local(2026, 9, 2, 11, 20), settings, 12, Local(2026, 9, 2, 11, 15), null);
    return plan.DueAt?.Hour == 12 && plan.DueAt?.Minute == 15;
});
Check("stops when the goal is reached", () => ReminderScheduler.GetPlan(Local(2026, 9, 2, 12, 0), settings, 80, null, null).DueAt is null);
Check("moves after-hours reminders to Monday", () =>
{
    var plan = ReminderScheduler.GetPlan(Local(2026, 9, 4, 18, 0), settings, 12, null, null);
    return plan.DueAt?.DayOfWeek == DayOfWeek.Monday && plan.DueAt?.Hour == 10;
});
Check("a sent notification advances the reminder", () => ReminderScheduler.GetPlan(Local(2026, 9, 2, 12, 1), settings, 12, null, Local(2026, 9, 2, 12, 0)).DueAt?.Hour == 13);

if (failures.Count > 0)
{
    Console.Error.WriteLine($"{failures.Count} reminder test(s) failed: {string.Join(", ", failures)}");
    return 1;
}
Console.WriteLine("All 5 native reminder tests passed.");
return 0;

void Check(string name, Func<bool> test)
{
    try { if (!test()) failures.Add(name); }
    catch (Exception exception) { failures.Add($"{name} ({exception.Message})"); }
}

static DateTimeOffset Local(int year, int month, int day, int hour, int minute) =>
    new(year, month, day, hour, minute, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(year, month, day, hour, minute, 0)));
