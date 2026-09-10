using System.Text.Json;
using System.Text.Json.Serialization;
using Waterline.Core;

namespace Waterline.Infrastructure;

public enum StateLoadStatus
{
    New,
    Loaded,
    Migrated,
    RecoveredFromBackup,
    Unrecoverable
}

public sealed record StateLoadResult(
    WaterlineState State,
    StateLoadStatus Status,
    bool CanSave,
    IReadOnlyList<string> Messages);

public sealed record StateSaveResult(bool Success, bool Blocked, string? Message)
{
    public static StateSaveResult Saved { get; } = new(true, false, null);
}

public sealed class AppStateStore
{
    private enum SaveBlockReason
    {
        None,
        RecoveredFromBackup,
        Unrecoverable
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _gate = new();
    private readonly string _filePath;
    private readonly string _backupPath;
    private readonly string _rollbackPath;
    private SaveBlockReason _saveBlockReason;
    private string? _migrationSourcePath;

    public AppStateStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Waterline",
            "state.json");
        _backupPath = Path.Combine(Path.GetDirectoryName(_filePath)!, "state.backup.json");
        _rollbackPath = Path.Combine(Path.GetDirectoryName(_filePath)!, "state.pre-schema-1.json");
    }

    public string FilePath => _filePath;
    public string RollbackPath => _rollbackPath;

    public StateLoadResult Load()
    {
        lock (_gate)
        {
            _saveBlockReason = SaveBlockReason.None;
            _migrationSourcePath = null;
            if (!File.Exists(_filePath) && !File.Exists(_backupPath))
                return new StateLoadResult(new WaterlineState(), StateLoadStatus.New, true, []);

            if (TryRead(_filePath, out var state, out var migrated, out var primaryMessages))
            {
                if (migrated) _migrationSourcePath = _filePath;
                return new StateLoadResult(
                    state!,
                    migrated ? StateLoadStatus.Migrated : StateLoadStatus.Loaded,
                    true,
                    primaryMessages);
            }

            if (TryRead(_backupPath, out state, out migrated, out var backupMessages))
            {
                if (migrated) _migrationSourcePath = _backupPath;
                _saveBlockReason = SaveBlockReason.RecoveredFromBackup;
                var messages = primaryMessages
                    .Concat(backupMessages)
                    .Append("Waterline recovered the last valid backup. Saving is blocked until recovery is acknowledged.")
                    .ToList();
                return new StateLoadResult(state!, StateLoadStatus.RecoveredFromBackup, false, messages);
            }

            _saveBlockReason = SaveBlockReason.Unrecoverable;
            return new StateLoadResult(
                new WaterlineState(),
                StateLoadStatus.Unrecoverable,
                false,
                primaryMessages.Concat(backupMessages).Append("No valid Waterline state could be loaded.").ToList());
        }
    }

    public StateSaveResult Save(WaterlineState state) => SaveCore(state, preserveCurrentForImport: false);

    public StateSaveResult SaveImportedState(WaterlineState state) => SaveCore(state, preserveCurrentForImport: true);

    private StateSaveResult SaveCore(WaterlineState state, bool preserveCurrentForImport)
    {
        lock (_gate)
        {
            if (_saveBlockReason != SaveBlockReason.None)
                return new StateSaveResult(false, true, "Saving is blocked until state recovery is resolved.");

            var validation = StateValidator.Validate(state);
            if (validation.Count > 0)
                return new StateSaveResult(false, false, string.Join(" ", validation));

            var directory = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = Path.Combine(directory, $"state.{Guid.NewGuid():N}.tmp");
            try
            {
                if (_migrationSourcePath is not null && !File.Exists(_rollbackPath))
                    PreserveCopy(_migrationSourcePath, _rollbackPath);
                if (preserveCurrentForImport && File.Exists(_filePath))
                {
                    var importBackup = Path.Combine(
                        directory,
                        $"state.pre-import.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}.json");
                    PreserveCopy(_filePath, importBackup);
                }

                var json = JsonSerializer.Serialize(state, JsonOptions);
                using (var stream = new FileStream(
                           temporaryPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           16 * 1024,
                           FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(_filePath))
                    File.Replace(temporaryPath, _filePath, _backupPath, true);
                else
                    File.Move(temporaryPath, _filePath);
                _migrationSourcePath = null;
                return StateSaveResult.Saved;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new StateSaveResult(false, false, exception.Message);
            }
            finally
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
            }
        }
    }

    private static void PreserveCopy(string sourcePath, string destinationPath)
    {
        var temporaryPath = destinationPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var destination = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       16 * 1024,
                       FileOptions.WriteThrough))
            {
                source.CopyTo(destination);
                destination.Flush(true);
            }
            File.Move(temporaryPath, destinationPath, false);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
        }
    }

    public bool AcknowledgeRecoveredState()
    {
        lock (_gate)
        {
            if (_saveBlockReason == SaveBlockReason.None) return true;
            if (_saveBlockReason != SaveBlockReason.RecoveredFromBackup) return false;
            try
            {
                if (File.Exists(_filePath))
                {
                    var directory = Path.GetDirectoryName(_filePath)!;
                    var quarantine = Path.Combine(directory, $"state.corrupt.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json");
                    File.Copy(_filePath, quarantine, false);
                }
                _saveBlockReason = SaveBlockReason.None;
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    private static bool TryRead(
        string path,
        out WaterlineState? state,
        out bool migrated,
        out IReadOnlyList<string> messages)
    {
        state = null;
        migrated = false;
        var notes = new List<string>();
        messages = notes;
        if (!File.Exists(path))
        {
            notes.Add($"State file was not found: {Path.GetFileName(path)}.");
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                notes.Add("State content must be a JSON object.");
                return false;
            }

            var version = 0;
            var hasVersion = TryGetProperty(root, "schemaVersion", out var versionElement);
            if (hasVersion && !versionElement.TryGetInt32(out version))
            {
                notes.Add("State schema version is invalid.");
                return false;
            }

            if (!hasVersion)
            {
                if (!HasSchemaZeroShape(root))
                {
                    notes.Add("State content does not match a supported legacy schema.");
                    return false;
                }
                state = MigrateSchemaZero(json, notes);
                migrated = true;
            }
            else if (version == StateSchema.CurrentVersion)
            {
                if (!HasSchemaOneShape(root))
                {
                    notes.Add("State content is missing required schema-one sections.");
                    return false;
                }
                state = JsonSerializer.Deserialize<WaterlineState>(json, JsonOptions);
            }
            else
            {
                notes.Add($"Unsupported state schema version {version}.");
                return false;
            }

            if (state is null)
            {
                notes.Add("State content was empty.");
                return false;
            }

            var validation = StateValidator.Validate(state);
            if (validation.Count > 0)
            {
                notes.AddRange(validation);
                state = null;
                return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            notes.Add($"Could not read {Path.GetFileName(path)}: {exception.Message}");
            state = null;
            return false;
        }
    }

    private static bool HasSchemaZeroShape(JsonElement root) =>
        TryGetProperty(root, "settings", out var settings) && settings.ValueKind == JsonValueKind.Object &&
        TryGetProperty(root, "drinks", out var drinks) && drinks.ValueKind == JsonValueKind.Array;

    private static bool HasSchemaOneShape(JsonElement root) =>
        HasSchemaZeroShape(root) &&
        TryGetProperty(root, "desktop", out var desktop) && desktop.ValueKind == JsonValueKind.Object &&
        TryGetProperty(root, "runtime", out var runtime) && runtime.ValueKind == JsonValueKind.Object;

    private static bool TryGetProperty(JsonElement root, string name, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            value = property.Value;
            return true;
        }
        value = default;
        return false;
    }

    private static WaterlineState MigrateSchemaZero(string json, List<string> notes)
    {
        var legacy = JsonSerializer.Deserialize<LegacyState>(json, JsonOptions) ?? new LegacyState();
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        var migratedEntries = new List<DrinkEntry>();
        var skipped = 0;
        var legacyDrinks = legacy.Drinks ?? [];
        for (var index = 0; index < legacyDrinks.Count; index++)
        {
            var entry = legacyDrinks[index];
            if (entry is null || !StateValidator.IsValidAmount(entry.AmountOz) || entry.At == default)
            {
                skipped++;
                continue;
            }
            var identifier = LegacyIdentifier(entry.Id, entry.At, index);
            while (!identifiers.Add(identifier)) identifier += $"-{index}";
            migratedEntries.Add(new DrinkEntry
            {
                Id = identifier,
                AmountOz = Math.Round(entry.AmountOz, 1),
                RecordedAt = entry.At
            });
        }

        if (skipped > 0) notes.Add($"Skipped {skipped} invalid legacy drink entr{(skipped == 1 ? "y" : "ies")}.");
        notes.Add("Migrated native Waterline schema zero in memory. The source file was not modified.");
        return new WaterlineState
        {
            SchemaVersion = StateSchema.CurrentVersion,
            Settings = legacy.Settings ?? new WaterlineSettings(),
            Drinks = migratedEntries,
            Runtime = new RuntimeState { LastNotificationAt = legacy.LastNotificationAt }
        };
    }

    private static string LegacyIdentifier(JsonElement id, DateTimeOffset at, int index)
    {
        var value = id.ValueKind switch
        {
            JsonValueKind.Number => id.GetRawText(),
            JsonValueKind.String => id.GetString(),
            _ => null
        };
        return $"v0-{value ?? at.ToUnixTimeMilliseconds().ToString()}-{index}";
    }

    private sealed class LegacyState
    {
        public WaterlineSettings? Settings { get; set; }
        public List<LegacyDrink?>? Drinks { get; set; } = [];
        public DateTimeOffset? LastNotificationAt { get; set; }
    }

    private sealed class LegacyDrink
    {
        public JsonElement Id { get; set; }
        public double AmountOz { get; set; }
        public DateTimeOffset At { get; set; }
    }
}
