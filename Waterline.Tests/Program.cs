using System.Text.Json;
using System.Text;
using LevelDB;
using Waterline.Core;
using Waterline.Infrastructure;

var tests = new List<(string Name, Action Run)>();
var failures = new List<string>();
var zone = TimeZoneInfo.CreateCustomTimeZone("Waterline-Test", TimeSpan.FromHours(-5), "Waterline Test", "Waterline Test");

Test("accepts the supported amount boundaries", () =>
{
    Equal(true, StateValidator.IsValidAmount(.1));
    Equal(true, StateValidator.IsValidAmount(64));
});

Test("rejects non-finite and rounded-zero amounts", () =>
{
    Equal(false, StateValidator.IsValidAmount(double.NaN));
    Equal(false, StateValidator.IsValidAmount(double.PositiveInfinity));
    Equal(false, StateValidator.IsValidAmount(.04));
    Equal(false, StateValidator.IsValidAmount(64.1));
});

Test("groups hydration by the supplied local timezone", () =>
{
    var entries = new[]
    {
        Drink(12, new DateTimeOffset(2026, 9, 9, 3, 30, 0, TimeSpan.Zero)),
        Drink(8, new DateTimeOffset(2026, 9, 9, 6, 30, 0, TimeSpan.Zero))
    };
    var progress = HydrationCalculator.GetProgress(entries, new DateOnly(2026, 9, 8), 80, zone);
    Near(12, progress.TotalOz);
    Near(15, progress.Percent);
});

Test("keeps zero-intake history at zero", () =>
{
    var days = HydrationCalculator.GetRecentDays([], new DateOnly(2026, 9, 8), 7, 80, zone);
    Equal(7, days.Count);
    Equal(true, days.All(day => day.TotalOz == 0 && !day.GoalReached));
});

Test("orders only the selected local day newest first", () =>
{
    var entries = new[]
    {
        Drink(8, new DateTimeOffset(2026, 9, 9, 3, 30, 0, TimeSpan.Zero)),
        Drink(12, new DateTimeOffset(2026, 9, 9, 6, 30, 0, TimeSpan.Zero)),
        Drink(16, new DateTimeOffset(2026, 9, 9, 8, 30, 0, TimeSpan.Zero))
    };
    var selected = HydrationCalculator.GetEntriesForDay(entries, new DateOnly(2026, 9, 8), zone);
    Equal(1, selected.Count);
    Near(8, selected[0].AmountOz);
});

Test("selects the latest eligible entry for current-day undo", () =>
{
    var day = new DateOnly(2026, 9, 8);
    var entries = new[]
    {
        Drink(8, Local(2026, 9, 8, 8, 15)),
        Drink(12, Local(2026, 9, 8, 11, 45)),
        Drink(16, Local(2026, 9, 7, 16, 0))
    };
    Near(12, HydrationCalculator.GetMostRecentEntry(entries, day, zone)!.AmountOz);
});

Test("reports goal completion and over-goal totals without clamping data", () =>
{
    var entries = new[] { Drink(92, Local(2026, 9, 8, 12, 0)) };
    var progress = HydrationCalculator.GetProgress(entries, new DateOnly(2026, 9, 8), 80, zone);
    Near(92, progress.TotalOz);
    Near(115, progress.Percent);
    Near(0, progress.RemainingOz);
    Equal(true, progress.IsComplete);
});

Test("recalculates history against a changed goal without changing drink amounts", () =>
{
    var entry = Drink(72, Local(2026, 9, 8, 12, 0));
    var first = HydrationCalculator.GetRecentDays([entry], new DateOnly(2026, 9, 8), 1, 80, zone).Single();
    var changed = HydrationCalculator.GetRecentDays([entry], new DateOnly(2026, 9, 8), 1, 64, zone).Single();
    Near(72, first.TotalOz);
    Near(72, changed.TotalOz);
    Equal(false, first.GoalReached);
    Equal(true, changed.GoalReached);
    Near(72, entry.AmountOz);
});

Test("calculates behind pace and a bounded next amount", () =>
{
    var pace = PaceCalculator.Calculate(Local(2026, 9, 8, 13, 0), StandardSettings(), 12, zone);
    Equal(PaceStatus.Behind, pace.Status);
    Equal(true, pace.SuggestedNextOz is >= 8 and <= 16);
});

Test("reports before, after, excluded and complete pace states", () =>
{
    var settings = StandardSettings();
    Equal(PaceStatus.BeforeSchedule, PaceCalculator.Calculate(Local(2026, 9, 8, 8, 30), settings, 0, zone).Status);
    Equal(PaceStatus.AfterSchedule, PaceCalculator.Calculate(Local(2026, 9, 8, 18, 0), settings, 24, zone).Status);
    Equal(PaceStatus.NoPlan, PaceCalculator.Calculate(Local(2026, 9, 6, 12, 0), settings, 24, zone).Status);
    Equal(PaceStatus.Complete, PaceCalculator.Calculate(Local(2026, 9, 8, 12, 0), settings, 80, zone).Status);
});

Test("schedules from workday start", () =>
{
    var plan = ReminderScheduler.GetPlan(Local(2026, 9, 8, 8, 30), StandardSettings(), 0, null, null, zone);
    Equal(10, TimeZoneInfo.ConvertTime(plan.DueAt!.Value, zone).Hour);
    Equal(false, plan.IsActive);
});

Test("logging a drink resets the reminder interval", () =>
{
    var plan = ReminderScheduler.GetPlan(
        Local(2026, 9, 8, 11, 20), StandardSettings(), 12, Local(2026, 9, 8, 11, 15), null, zone);
    var localDue = TimeZoneInfo.ConvertTime(plan.DueAt!.Value, zone);
    Equal(12, localDue.Hour);
    Equal(15, localDue.Minute);
});

Test("a sent reminder advances the next reminder", () =>
{
    var plan = ReminderScheduler.GetPlan(
        Local(2026, 9, 8, 12, 1), StandardSettings(), 12, null, Local(2026, 9, 8, 12, 0), zone);
    Equal(13, TimeZoneInfo.ConvertTime(plan.DueAt!.Value, zone).Hour);
});

Test("reminders stop at goal and invalid schedules", () =>
{
    var settings = StandardSettings();
    Equal<DateTimeOffset?>(null, ReminderScheduler.GetPlan(Local(2026, 9, 8, 12, 0), settings, 80, null, null, zone).DueAt);
    settings.WorkdayEnd = settings.WorkdayStart;
    Equal<DateTimeOffset?>(null, ReminderScheduler.GetPlan(Local(2026, 9, 8, 12, 0), settings, 0, null, null, zone).DueAt);
});

Test("after-hours reminders move to the next selected day", () =>
{
    var plan = ReminderScheduler.GetPlan(Local(2026, 9, 11, 18, 0), StandardSettings(), 12, null, null, zone);
    var localDue = TimeZoneInfo.ConvertTime(plan.DueAt!.Value, zone);
    Equal(DayOfWeek.Monday, localDue.DayOfWeek);
    Equal(10, localDue.Hour);
});

Test("normalizes a reminder start that falls in the daylight-saving gap", () =>
{
    var daylightZone = DaylightZone();
    var settings = StandardSettings();
    settings.ReminderDays = [DayOfWeek.Sunday];
    settings.WorkdayStart = new TimeSpan(2, 0, 0);
    settings.WorkdayEnd = new TimeSpan(5, 0, 0);
    var beforeGap = new DateTimeOffset(2026, 3, 8, 0, 30, 0, TimeSpan.FromHours(-6));
    var plan = ReminderScheduler.GetPlan(beforeGap, settings, 0, null, null, daylightZone);
    var localDue = TimeZoneInfo.ConvertTime(plan.DueAt!.Value, daylightZone);
    Equal(4, localDue.Hour);
    Equal(TimeSpan.FromHours(-5), localDue.Offset);
});

Test("validates duplicate identifiers and invalid settings", () =>
{
    var state = new WaterlineState
    {
        Settings = new WaterlineSettings { DailyGoalOz = double.NaN },
        Drinks =
        [
            new DrinkEntry { Id = "same", AmountOz = 8 },
            new DrinkEntry { Id = "same", AmountOz = 12 }
        ]
    };
    var errors = StateValidator.Validate(state);
    Equal(true, errors.Count >= 2);
});

Test("loads a new profile without creating directories", () =>
{
    using var temp = new TemporaryDirectory();
    var statePath = Path.Combine(temp.Path, "profile", "state.json");
    var result = new AppStateStore(statePath).Load();
    Equal(StateLoadStatus.New, result.Status);
    Equal(true, result.CanSave);
    Equal(false, Directory.Exists(Path.GetDirectoryName(statePath)));
});

Test("migrates the v2 unversioned state without modifying its source", () =>
{
    using var temp = new TemporaryDirectory();
    var statePath = Path.Combine(temp.Path, "state.json");
    var original = LegacyJson();
    File.WriteAllText(statePath, original);
    var store = new AppStateStore(statePath);
    var result = store.Load();
    Equal(StateLoadStatus.Migrated, result.Status);
    Equal(true, result.CanSave);
    Equal(2, result.State.Drinks.Count);
    Equal(new DateTimeOffset(2026, 9, 8, 9, 15, 0, TimeSpan.FromHours(-5)), result.State.Drinks[0].RecordedAt);
    Equal(original, File.ReadAllText(statePath));
});

Test("saving migrated state writes schema one and preserves values", () =>
{
    using var temp = new TemporaryDirectory();
    var statePath = Path.Combine(temp.Path, "state.json");
    var original = LegacyJson();
    File.WriteAllText(statePath, original);
    var store = new AppStateStore(statePath);
    var migrated = store.Load();
    Equal(true, store.Save(migrated.State).Success);
    Equal(original, File.ReadAllText(store.RollbackPath));
    using var json = JsonDocument.Parse(File.ReadAllText(statePath));
    Equal(StateSchema.CurrentVersion, json.RootElement.GetProperty("schemaVersion").GetInt32());
    var reloaded = new AppStateStore(statePath).Load();
    Equal(StateLoadStatus.Loaded, reloaded.Status);
    Near(80, reloaded.State.Settings.DailyGoalOz);
    Equal(2, reloaded.State.Drinks.Count);
});

Test("preserves the first rollback copy across later schema-one saves", () =>
{
    using var temp = new TemporaryDirectory();
    var statePath = Path.Combine(temp.Path, "state.json");
    var original = LegacyJson();
    File.WriteAllText(statePath, original);
    var store = new AppStateStore(statePath);
    var migrated = store.Load();
    Equal(true, store.Save(migrated.State).Success);
    migrated.State.Drinks.Add(Drink(4, new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.FromHours(-5))));
    Equal(true, store.Save(migrated.State).Success);
    Equal(original, File.ReadAllText(store.RollbackPath));
});

Test("refuses to coerce an unsupported future schema during save", () =>
{
    using var temp = new TemporaryDirectory();
    var statePath = Path.Combine(temp.Path, "state.json");
    var state = ValidState(8);
    state.SchemaVersion = StateSchema.CurrentVersion + 1;
    var result = new AppStateStore(statePath).Save(state);
    Equal(false, result.Success);
    Equal(false, File.Exists(statePath));
    Equal(StateSchema.CurrentVersion + 1, state.SchemaVersion);
});

Test("atomic replacement retains the previous valid state as backup", () =>
{
    using var temp = new TemporaryDirectory();
    var statePath = Path.Combine(temp.Path, "state.json");
    var store = new AppStateStore(statePath);
    Equal(true, store.Save(ValidState(8)).Success);
    Equal(true, store.Save(ValidState(20)).Success);
    var backup = new AppStateStore(Path.Combine(temp.Path, "state.backup.json")).Load();
    Near(8, backup.State.Drinks.Single().AmountOz);
    Equal(0, Directory.GetFiles(temp.Path, "*.tmp").Length);
});

Test("serializes concurrent save requests without leaving temporary files", () =>
{
    using var temp = new TemporaryDirectory();
    var statePath = Path.Combine(temp.Path, "state.json");
    var store = new AppStateStore(statePath);
    var results = new bool[20];
    Parallel.For(0, results.Length, index =>
    {
        results[index] = store.Save(ValidState(index + 1)).Success;
    });
    Equal(true, results.All(result => result));
    Equal(StateLoadStatus.Loaded, new AppStateStore(statePath).Load().Status);
    Equal(0, Directory.GetFiles(temp.Path, "*.tmp").Length);
});

Test("recovers a valid backup and blocks writes until acknowledged", () =>
{
    using var temp = new TemporaryDirectory();
    var statePath = Path.Combine(temp.Path, "state.json");
    var store = new AppStateStore(statePath);
    Equal(true, store.Save(ValidState(8)).Success);
    Equal(true, store.Save(ValidState(20)).Success);
    File.WriteAllText(statePath, "{broken");

    var recoveryStore = new AppStateStore(statePath);
    var recovered = recoveryStore.Load();
    Equal(StateLoadStatus.RecoveredFromBackup, recovered.Status);
    Equal(false, recovered.CanSave);
    Equal(true, recoveryStore.Save(recovered.State).Blocked);
    Equal(true, recoveryStore.AcknowledgeRecoveredState());
    Equal(1, Directory.GetFiles(temp.Path, "state.corrupt.*.json").Length);
    Equal(true, recoveryStore.Save(recovered.State).Success);
});

Test("unrecoverable state never silently overwrites existing files", () =>
{
    using var temp = new TemporaryDirectory();
    var statePath = Path.Combine(temp.Path, "state.json");
    var backupPath = Path.Combine(temp.Path, "state.backup.json");
    File.WriteAllText(statePath, "not-json");
    File.WriteAllText(backupPath, "also-not-json");
    var store = new AppStateStore(statePath);
    var result = store.Load();
    Equal(StateLoadStatus.Unrecoverable, result.Status);
    Equal(false, result.CanSave);
    Equal(true, store.Save(result.State).Blocked);
    Equal(false, store.AcknowledgeRecoveredState());
    Equal(true, store.Save(result.State).Blocked);
    Equal("not-json", File.ReadAllText(statePath));
    Equal("also-not-json", File.ReadAllText(backupPath));
});

Test("valid JSON with an unknown shape is not treated as legacy state", () =>
{
    using var temp = new TemporaryDirectory();
    var statePath = Path.Combine(temp.Path, "state.json");
    File.WriteAllText(statePath, "{}");
    var store = new AppStateStore(statePath);
    var result = store.Load();
    Equal(StateLoadStatus.Unrecoverable, result.Status);
    Equal(false, store.AcknowledgeRecoveredState());
    Equal("{}", File.ReadAllText(statePath));
});

Test("schema one requires all persisted sections", () =>
{
    using var temp = new TemporaryDirectory();
    var statePath = Path.Combine(temp.Path, "state.json");
    File.WriteAllText(statePath, "{\"schemaVersion\":1,\"settings\":{},\"drinks\":[]}");
    var result = new AppStateStore(statePath).Load();
    Equal(StateLoadStatus.Unrecoverable, result.Status);
    Equal(false, result.CanSave);
});

Test("legacy Electron discovery is read-only", () =>
{
    using var temp = new TemporaryDirectory();
    var levelDb = Path.Combine(temp.Path, "Waterline", "Local Storage", "leveldb");
    Directory.CreateDirectory(levelDb);
    var before = Directory.GetDirectories(temp.Path, "*", SearchOption.AllDirectories).Length;
    var locations = new LegacyDataDiscovery(temp.Path).FindElectronLocations();
    Equal(true, locations.Any(location => location.Exists && location.LevelDbPath == levelDb));
    Equal(before, Directory.GetDirectories(temp.Path, "*", SearchOption.AllDirectories).Length);
});

Test("imports a copied Electron LevelDB profile without changing its source", () =>
{
    using var temp = new TemporaryDirectory();
    var levelDb = Path.Combine(temp.Path, "leveldb");
    using (var database = new DB(new Options { CreateIfMissing = true }, levelDb))
    {
        database.Put(ChromiumKey("waterline-settings"), ChromiumValue("{\"goal\":96,\"interval\":45,\"start\":\"08:30\",\"end\":\"18:00\",\"reminders\":true,\"weekdays\":[1,3,5],\"sounds\":false}"));
        database.Put(ChromiumKey("waterline-drinks-2026-09-08"), ChromiumValue("[{\"id\":1001,\"amount\":12,\"at\":\"2026-09-08T09:15:00-05:00\"},{\"id\":1001,\"amount\":12,\"at\":\"2026-09-08T09:15:00-05:00\"},{\"id\":1002,\"amount\":0,\"at\":\"bad\"}]"));
    }
    var sourceBefore = Directory.GetFiles(levelDb).ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes);

    var snapshot = new LegacyDataImporter().Read(levelDb);
    Equal(1, snapshot.Drinks.Count);
    Equal(1, snapshot.SkippedRecords);
    Near(96, snapshot.Settings!.DailyGoalOz!.Value);
    Equal(45, snapshot.Settings.ReminderIntervalMinutes!.Value);
    Equal(false, snapshot.Settings.SoundsEnabled!.Value);
    foreach (var source in sourceBefore)
        Equal(true, source.Value.SequenceEqual(File.ReadAllBytes(Path.Combine(levelDb, source.Key!))));

    var state = ValidState(8);
    var first = LegacyDataImporter.MergeInto(state, snapshot);
    Equal(1, first.ImportedDrinks);
    Equal(true, first.SettingsImported);
    var repeated = LegacyDataImporter.MergeInto(state, snapshot);
    Equal(0, repeated.ImportedDrinks);
    Equal(1, repeated.DuplicateDrinks);
});

Test("merges only settings fields present in a partial legacy record", () =>
{
    var snapshot = LegacyDataImporter.ParseRecords([
        new KeyValuePair<byte[], byte[]>(ChromiumKey("waterline-settings"), ChromiumValue("{\"sounds\":false}"))
    ]);
    var state = ValidState(8);
    state.Settings.DailyGoalOz = 120;
    state.Settings.ReminderIntervalMinutes = 90;
    state.Settings.SoundsEnabled = true;

    var result = LegacyDataImporter.MergeInto(state, snapshot);

    Equal(true, result.SettingsImported);
    Near(120, state.Settings.DailyGoalOz);
    Equal(90, state.Settings.ReminderIntervalMinutes);
    Equal(false, state.Settings.SoundsEnabled);
});

Test("creates a unique pre-import backup before merging state", () =>
{
    using var temp = new TemporaryDirectory();
    var statePath = Path.Combine(temp.Path, "state.json");
    var store = new AppStateStore(statePath);
    Equal(true, store.Save(ValidState(8)).Success);
    var original = File.ReadAllText(statePath);
    Equal(true, store.SaveImportedState(ValidState(20)).Success);
    var backups = Directory.GetFiles(temp.Path, "state.pre-import.*.json");
    Equal(1, backups.Length);
    Equal(original, File.ReadAllText(backups[0]));
});

Test("accepts only the version-matched installer from the official repository", () =>
{
    var version = new Version(2, 1, 0);
    Equal(true, UpdateAssetPolicy.TryGetTrustedInstallerUri(
        "https://github.com/AayyKay/waterline/releases/download/v2.1.0/Waterline-Setup-2.1.0.exe",
        "Waterline-Setup-2.1.0.exe", version, out var uri));
    Equal("github.com", uri!.Host);
});

Test("rejects update assets with the wrong host repository name or version", () =>
{
    var version = new Version(2, 1, 0);
    Equal(false, UpdateAssetPolicy.TryGetTrustedInstallerUri(
        "https://example.com/AayyKay/waterline/releases/download/v2.1.0/Waterline-Setup-2.1.0.exe",
        "Waterline-Setup-2.1.0.exe", version, out _));
    Equal(false, UpdateAssetPolicy.TryGetTrustedInstallerUri(
        "https://github.com/someone/waterline/releases/download/v2.1.0/Waterline-Setup-2.1.0.exe",
        "Waterline-Setup-2.1.0.exe", version, out _));
    Equal(false, UpdateAssetPolicy.TryGetTrustedInstallerUri(
        "https://github.com/AayyKay/waterline/releases/download/v2.1.0/Waterline-Setup-9.9.9.exe",
        "Waterline-Setup-9.9.9.exe", version, out _));
});

Test("accepts only official Waterline release pages", () =>
{
    Equal(true, UpdateAssetPolicy.IsTrustedReleasePage("https://github.com/AayyKay/waterline/releases/tag/v2.1.0"));
    Equal(false, UpdateAssetPolicy.IsTrustedReleasePage("https://github.com/another/waterline/releases/tag/v2.1.0"));
    Equal(false, UpdateAssetPolicy.IsTrustedReleasePage("http://github.com/AayyKay/waterline/releases/latest"));
});

Test("normalizes and enforces GitHub SHA-256 installer digests", () =>
{
    using var temp = new TemporaryDirectory();
    var path = Path.Combine(temp.Path, "installer.exe");
    File.WriteAllText(path, "verified Waterline installer bytes");
    var expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    Equal(true, UpdateAssetPolicy.TryNormalizeSha256($"sha256:{expected.ToUpperInvariant()}", out var normalized));
    Equal(expected, normalized);
    Equal(true, UpdateAssetPolicy.HasExpectedSha256(path, expected));
    File.AppendAllText(path, "tampered");
    Equal(false, UpdateAssetPolicy.HasExpectedSha256(path, expected));
    Equal(false, UpdateAssetPolicy.TryNormalizeSha256("sha256:not-a-digest", out _));
});

Test("captures widget placement as normalized work-area anchors", () =>
{
    var area = new WidgetWorkArea("DISPLAY2", 1920, 0, 2560, 1400);
    var captured = WidgetPlacementPolicy.Capture(new WidgetBounds(4080, 900, 400, 500), area, 1.5);
    Equal("DISPLAY2", captured.MonitorId);
    Near(1, captured.AnchorX);
    Near(1, captured.AnchorY);
    Near(1.5, captured.DpiScale);
});

Test("restores widget to a fallback monitor and clamps its size", () =>
{
    var placement = new WidgetPlacement { MonitorId = "REMOVED", AnchorX = 1, AnchorY = 1 };
    var areas = new[]
    {
        new WidgetWorkArea("PRIMARY", 0, 0, 1200, 800),
        new WidgetWorkArea("SECONDARY", -900, 0, 900, 700)
    };
    var restored = WidgetPlacementPolicy.Restore(placement, areas, "PRIMARY", 1400, 900);
    Near(0, restored.Left);
    Near(0, restored.Top);
    Near(1200, restored.Width);
    Near(800, restored.Height);
});

Test("rejects unsupported widget modes and invalid placement", () =>
{
    var state = ValidState(12);
    state.Desktop.WidgetMode = "floating";
    state.Desktop.WidgetPlacement = new WidgetPlacement { AnchorX = double.NaN };
    var errors = StateValidator.Validate(state);
    Equal(true, errors.Contains("Widget mode is not supported."));
    Equal(true, errors.Contains("Widget placement is invalid."));
});

Test("builds distinct bounded Waterline audio cues", () =>
{
    var log = WaterlineAudio.CreateLogCue();
    var reminder = WaterlineAudio.CreateReminderCue();
    Equal("RIFF", Encoding.ASCII.GetString(log, 0, 4));
    Equal("WAVE", Encoding.ASCII.GetString(reminder, 8, 4));
    Near(.27, WaveDuration(log), .002);
    Near(.66, WaveDuration(reminder), .002);
    Equal(true, WavePeak(log) is > 1200 and <= 9200);
    Equal(true, WavePeak(reminder) is > 1200 and <= 9200);
    Equal(false, log.SequenceEqual(reminder));
});

foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{test.Name}: {exception.Message}");
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"{failures.Count} of {tests.Count} tests failed.");
    return 1;
}

Console.WriteLine($"All {tests.Count} automated Waterline checks passed.");
return 0;

void Test(string name, Action run) => tests.Add((name, run));

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected {expected}, received {actual}.");
}

static void Near(double expected, double actual, double tolerance = .001)
{
    if (Math.Abs(expected - actual) > tolerance)
        throw new InvalidOperationException($"Expected {expected}, received {actual}.");
}

static double WaveDuration(byte[] wave) => BitConverter.ToInt32(wave, 40) / 2d / BitConverter.ToInt32(wave, 24);

static int WavePeak(byte[] wave)
{
    var peak = 0;
    for (var offset = 44; offset + 1 < wave.Length; offset += 2)
        peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(wave, offset)));
    return peak;
}

static byte[] ChromiumKey(string key)
{
    var prefix = Encoding.UTF8.GetBytes("_file://waterline\0");
    return prefix.Concat(ChromiumValue(key)).ToArray();
}

static byte[] ChromiumValue(string value) => [1, .. Encoding.Latin1.GetBytes(value)];

static DrinkEntry Drink(double amount, DateTimeOffset at) =>
    new() { Id = Guid.NewGuid().ToString("N"), AmountOz = amount, RecordedAt = at };

static WaterlineSettings StandardSettings() => new()
{
    DailyGoalOz = 80,
    ReminderIntervalMinutes = 60,
    WorkdayStart = new TimeSpan(9, 0, 0),
    WorkdayEnd = new TimeSpan(17, 0, 0),
    RemindersEnabled = true,
    ReminderDays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]
};

static TimeZoneInfo DaylightZone()
{
    var start = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
        new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday);
    var end = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
        new DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday);
    var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
        new DateTime(2020, 1, 1), new DateTime(2030, 12, 31), TimeSpan.FromHours(1), start, end);
    return TimeZoneInfo.CreateCustomTimeZone(
        "Waterline-DST-Test",
        TimeSpan.FromHours(-6),
        "Waterline DST Test",
        "Waterline Standard",
        "Waterline Daylight",
        [rule]);
}

DateTimeOffset Local(int year, int month, int day, int hour, int minute)
{
    var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
    return new DateTimeOffset(local, zone.GetUtcOffset(local));
}

static WaterlineState ValidState(double amount) => new()
{
    Drinks =
    [
        new DrinkEntry
        {
            Id = $"drink-{amount:0.#}",
            AmountOz = amount,
            RecordedAt = new DateTimeOffset(2026, 9, 8, 9, 15, 0, TimeSpan.FromHours(-5))
        }
    ]
};

static string LegacyJson() => """
{
  "Settings": {
    "DailyGoalOz": 80,
    "ReminderIntervalMinutes": 60,
    "WorkdayStart": "09:00:00",
    "WorkdayEnd": "17:00:00",
    "RemindersEnabled": true,
    "SoundsEnabled": true,
    "ReminderDays": [1, 2, 3, 4, 5]
  },
  "Drinks": [
    { "Id": 1001, "AmountOz": 12, "At": "2026-09-08T09:15:00-05:00" },
    { "Id": 1002, "AmountOz": 8, "At": "2026-09-08T10:30:00-05:00" }
  ],
  "LastNotificationAt": "2026-09-08T08:00:00-05:00"
}
""";

sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Waterline.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path)) Directory.Delete(Path, true);
    }
}
