using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LevelDB;
using Waterline.Core;

namespace Waterline.Infrastructure;

public sealed record LegacyImportSnapshot(
    LegacySettingsPatch? Settings,
    IReadOnlyList<DrinkEntry> Drinks,
    int SkippedRecords);

public sealed record LegacySettingsPatch(
    double? DailyGoalOz,
    int? ReminderIntervalMinutes,
    TimeSpan? WorkdayStart,
    TimeSpan? WorkdayEnd,
    bool? RemindersEnabled,
    bool? SoundsEnabled,
    IReadOnlySet<DayOfWeek>? ReminderDays);

public sealed record LegacyMergeResult(int ImportedDrinks, int DuplicateDrinks, int SkippedRecords, bool SettingsImported);

public sealed class LegacyDataImporter
{
    private const string SettingsKey = "waterline-settings";
    private const string DrinksPrefix = "waterline-drinks-";

    public LegacyImportSnapshot Read(string levelDbPath)
    {
        if (!Directory.Exists(levelDbPath))
            throw new DirectoryNotFoundException("The older Waterline data folder is no longer available.");

        var scratch = Path.Combine(Path.GetTempPath(), "Waterline", "LegacyImport", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            foreach (var sourcePath in Directory.EnumerateFiles(levelDbPath))
            {
                if (Path.GetFileName(sourcePath).Equals("LOCK", StringComparison.OrdinalIgnoreCase)) continue;
                CopyShared(sourcePath, Path.Combine(scratch, Path.GetFileName(sourcePath)));
            }

            using var database = new DB(new Options { CreateIfMissing = false, ParanoidChecks = true }, scratch);
            using var iterator = database.CreateIterator(new ReadOptions { VerifyCheckSums = true, FillCache = false });
            var records = new List<KeyValuePair<byte[], byte[]>>();
            for (iterator.SeekToFirst(); iterator.IsValid(); iterator.Next())
                records.Add(new KeyValuePair<byte[], byte[]>(iterator.Key(), iterator.Value()));
            return ParseRecords(records);
        }
        finally
        {
            try { Directory.Delete(scratch, true); } catch { }
        }
    }

    public static LegacyImportSnapshot ParseRecords(IEnumerable<KeyValuePair<byte[], byte[]>> records)
    {
        LegacySettingsPatch? settings = null;
        var drinks = new Dictionary<string, DrinkEntry>(StringComparer.Ordinal);
        var skipped = 0;

        foreach (var record in records)
        {
            if (!TryDecodeRecord(record.Key, record.Value, out var key, out var value)) continue;
            if (key.Equals(SettingsKey, StringComparison.Ordinal))
            {
                if (TryParseSettings(value, out var parsed)) settings = parsed;
                else skipped++;
                continue;
            }
            if (!key.StartsWith(DrinksPrefix, StringComparison.Ordinal)) continue;
            if (!DateOnly.TryParseExact(key[DrinksPrefix.Length..], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                skipped++;
                continue;
            }

            try
            {
                using var json = JsonDocument.Parse(value);
                if (json.RootElement.ValueKind != JsonValueKind.Array)
                {
                    skipped++;
                    continue;
                }

                foreach (var item in json.RootElement.EnumerateArray())
                {
                    if (!TryParseDrink(item, out var entry))
                    {
                        skipped++;
                        continue;
                    }
                    drinks.TryAdd(entry.Id, entry);
                }
            }
            catch (JsonException)
            {
                skipped++;
            }
        }

        return new LegacyImportSnapshot(settings, drinks.Values.OrderBy(entry => entry.RecordedAt).ToList(), skipped);
    }

    public static LegacyMergeResult MergeInto(WaterlineState target, LegacyImportSnapshot snapshot)
    {
        var identifiers = target.Drinks.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        var imported = 0;
        var duplicates = 0;
        foreach (var entry in snapshot.Drinks)
        {
            if (!identifiers.Add(entry.Id))
            {
                duplicates++;
                continue;
            }
            target.Drinks.Add(entry);
            imported++;
        }

        if (snapshot.Settings is { } importedSettings)
        {
            if (importedSettings.DailyGoalOz is { } goal) target.Settings.DailyGoalOz = goal;
            if (importedSettings.ReminderIntervalMinutes is { } interval) target.Settings.ReminderIntervalMinutes = interval;
            if (importedSettings.WorkdayStart is { } start) target.Settings.WorkdayStart = start;
            if (importedSettings.WorkdayEnd is { } end) target.Settings.WorkdayEnd = end;
            if (importedSettings.RemindersEnabled is { } reminders) target.Settings.RemindersEnabled = reminders;
            if (importedSettings.SoundsEnabled is { } sounds) target.Settings.SoundsEnabled = sounds;
            if (importedSettings.ReminderDays is { } days) target.Settings.ReminderDays = [.. days];
        }

        return new LegacyMergeResult(imported, duplicates, snapshot.SkippedRecords, snapshot.Settings is not null);
    }

    private static bool TryDecodeRecord(byte[] rawKey, byte[] rawValue, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;
        if (rawKey.Length < 4 || rawKey[0] != (byte)'_') return false;
        var separator = Array.IndexOf(rawKey, (byte)0, 1);
        if (separator < 0 || separator + 1 >= rawKey.Length) return false;
        if (!TryDecodeChromiumString(rawKey.AsSpan(separator + 1), out key)) return false;
        return TryDecodeChromiumString(rawValue, out value);
    }

    private static bool TryDecodeChromiumString(ReadOnlySpan<byte> bytes, out string value)
    {
        value = string.Empty;
        if (bytes.Length == 0) return false;
        if (bytes[0] == 1)
        {
            value = Encoding.Latin1.GetString(bytes[1..]);
            return true;
        }
        if (bytes[0] == 0 && (bytes.Length - 1) % 2 == 0)
        {
            value = Encoding.Unicode.GetString(bytes[1..]);
            return true;
        }
        return false;
    }

    private static bool TryParseSettings(string value, out LegacySettingsPatch settings)
    {
        settings = new LegacySettingsPatch(null, null, null, null, null, null, null);
        try
        {
            using var json = JsonDocument.Parse(value);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;

            double? goalValue = null;
            if (root.TryGetProperty("goal", out var goal) && goal.TryGetDouble(out var parsedGoal) &&
                double.IsFinite(parsedGoal) && parsedGoal is >= 8 and <= 512)
                goalValue = Math.Round(parsedGoal, 1);
            int? intervalValue = null;
            if (root.TryGetProperty("interval", out var interval) && interval.TryGetInt32(out var parsedInterval) &&
                parsedInterval is 30 or 45 or 60 or 90 or 120)
                intervalValue = parsedInterval;
            TimeSpan? startValue = null;
            TimeSpan? endValue = null;
            if (root.TryGetProperty("start", out var start) && TryParseTime(start.GetString(), out var parsedStart) &&
                root.TryGetProperty("end", out var end) && TryParseTime(end.GetString(), out var parsedEnd) &&
                parsedStart < parsedEnd)
            {
                startValue = parsedStart;
                endValue = parsedEnd;
            }
            bool? remindersValue = null;
            if (root.TryGetProperty("reminders", out var reminders) && reminders.ValueKind is JsonValueKind.True or JsonValueKind.False)
                remindersValue = reminders.GetBoolean();
            bool? soundsValue = null;
            if (root.TryGetProperty("sounds", out var sounds) && sounds.ValueKind is JsonValueKind.True or JsonValueKind.False)
                soundsValue = sounds.GetBoolean();
            IReadOnlySet<DayOfWeek>? reminderDays = null;
            if (root.TryGetProperty("weekdays", out var weekdays) && weekdays.ValueKind == JsonValueKind.Array)
            {
                reminderDays = weekdays.EnumerateArray()
                    .Where(day => day.TryGetInt32(out var number) && number is >= 0 and <= 6)
                    .Select(day => (DayOfWeek)day.GetInt32())
                    .ToHashSet();
            }
            settings = new LegacySettingsPatch(
                goalValue,
                intervalValue,
                startValue,
                endValue,
                remindersValue,
                soundsValue,
                reminderDays);
            return goalValue is not null || intervalValue is not null || startValue is not null ||
                   remindersValue is not null || soundsValue is not null || reminderDays is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseDrink(JsonElement item, out DrinkEntry entry)
    {
        entry = new DrinkEntry();
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("amount", out var amount) || !amount.TryGetDouble(out var amountValue) ||
            !StateValidator.IsValidAmount(amountValue) ||
            !item.TryGetProperty("at", out var at) ||
            !DateTimeOffset.TryParse(at.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var recordedAt))
            return false;

        var legacyId = item.TryGetProperty("id", out var id) ? id.GetRawText() : string.Empty;
        var identity = $"{legacyId}|{Math.Round(amountValue, 1):0.0}|{recordedAt:O}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
        entry = new DrinkEntry
        {
            Id = $"electron-{hash[..24]}",
            AmountOz = Math.Round(amountValue, 1),
            RecordedAt = recordedAt
        };
        return true;
    }

    private static bool TryParseTime(string? value, out TimeSpan result) =>
        TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out result);

    private static void CopyShared(string sourcePath, string destinationPath)
    {
        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        source.CopyTo(destination);
        destination.Flush(true);
    }
}
