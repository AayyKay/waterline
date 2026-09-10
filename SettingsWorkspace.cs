using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.IO;
using Waterline.Core;
using Waterline.Infrastructure;

namespace Waterline;

public enum UpdateExperienceState
{
    Idle,
    Checking,
    Current,
    Available,
    Downloading,
    Ready,
    Installing,
    Failed,
    Unavailable
}

public sealed class ConfigurationViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MainViewModel _main;
    private readonly IUpdateService _updates;
    private readonly DiagnosticLog _diagnostics;
    private bool _scheduleRemindersEnabled;
    private int _scheduleIntervalMinutes;
    private string _scheduleStart = string.Empty;
    private string _scheduleEnd = string.Empty;
    private bool _monday;
    private bool _tuesday;
    private bool _wednesday;
    private bool _thursday;
    private bool _friday;
    private bool _saturday;
    private bool _sunday;
    private string _acceptedSchedule = string.Empty;
    private string _scheduleError = string.Empty;
    private string _scheduleNotice = string.Empty;
    private string _goalText = string.Empty;
    private bool _soundsEnabled;
    private string _widgetMode = "expanded";
    private string _acceptedSettings = string.Empty;
    private string _settingsError = string.Empty;
    private string _settingsNotice = string.Empty;
    private UpdateExperienceState _updateState;
    private string _updateStatus = "Ready to check";
    private string _updateDetail = "Waterline checks only the official GitHub Releases endpoint.";
    private double _updateProgress;
    private ReleaseInfo? _release;
    private string? _installerPath;
    private bool _updateInitialized;
    private CancellationTokenSource? _updateCancellation;
    private readonly string? _legacyDataPath;
    private string _legacyImportStatus;
    private bool _isImportingLegacy;

    public ConfigurationViewModel(MainViewModel main, IUpdateService updates, DiagnosticLog diagnostics, bool enableLegacyDiscovery = true)
    {
        _main = main;
        _updates = updates;
        _diagnostics = diagnostics;
        _legacyDataPath = enableLegacyDiscovery
            ? new LegacyDataDiscovery().FindElectronLocations()
                .FirstOrDefault(location => location.Exists)?.LevelDbPath
            : null;
        _legacyImportStatus = _legacyDataPath is null
            ? "No Electron-era Waterline data was found on this PC."
            : "An Electron-era Waterline profile is available. Importing merges valid settings and entries without changing the older files.";
#if DEBUG
        _updateState = UpdateExperienceState.Unavailable;
        _updateStatus = "Unavailable in development";
        _updateDetail = "Update checks are disabled in development builds.";
#else
        _updateState = UpdateExperienceState.Idle;
#endif
        ReloadSchedule();
        ReloadSettings();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<int> ReminderIntervals { get; } = [30, 45, 60, 90, 120];
    public IReadOnlyList<string> WidgetModes { get; } = ["expanded", "compact"];

    public bool ScheduleRemindersEnabled { get => _scheduleRemindersEnabled; set => SetSchedule(ref _scheduleRemindersEnabled, value); }
    public int ScheduleIntervalMinutes { get => _scheduleIntervalMinutes; set => SetSchedule(ref _scheduleIntervalMinutes, value); }
    public string ScheduleStart { get => _scheduleStart; set => SetSchedule(ref _scheduleStart, value); }
    public string ScheduleEnd { get => _scheduleEnd; set => SetSchedule(ref _scheduleEnd, value); }
    public bool Monday { get => _monday; set => SetSchedule(ref _monday, value); }
    public bool Tuesday { get => _tuesday; set => SetSchedule(ref _tuesday, value); }
    public bool Wednesday { get => _wednesday; set => SetSchedule(ref _wednesday, value); }
    public bool Thursday { get => _thursday; set => SetSchedule(ref _thursday, value); }
    public bool Friday { get => _friday; set => SetSchedule(ref _friday, value); }
    public bool Saturday { get => _saturday; set => SetSchedule(ref _saturday, value); }
    public bool Sunday { get => _sunday; set => SetSchedule(ref _sunday, value); }
    public bool HasScheduleChanges => ScheduleSignature() != _acceptedSchedule;
    public string ScheduleError => _scheduleError;
    public bool HasScheduleError => !string.IsNullOrWhiteSpace(_scheduleError);
    public string ScheduleNotice => _scheduleNotice;
    public bool HasScheduleNotice => !string.IsNullOrWhiteSpace(_scheduleNotice);
    public string SchedulePreview => TryBuildSchedule(out var draft, out var error)
        ? _main.PreviewReminder(draft)
        : error;

    public string GoalText { get => _goalText; set => SetSetting(ref _goalText, value); }
    public bool SoundsEnabled { get => _soundsEnabled; set => SetSetting(ref _soundsEnabled, value); }
    public string WidgetMode { get => _widgetMode; set => SetSetting(ref _widgetMode, value); }
    public bool HasSettingsChanges => SettingsSignature() != _acceptedSettings;
    public string SettingsError => _settingsError;
    public bool HasSettingsError => !string.IsNullOrWhiteSpace(_settingsError);
    public string SettingsNotice => _settingsNotice;
    public bool HasSettingsNotice => !string.IsNullOrWhiteSpace(_settingsNotice);

    public string VersionLabel => $"Waterline {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "development"}";
    public string DataLocation => _main.StateFilePath;
    public string RecoveryStatus => _main.RecoveryStatusLabel;
    public string DiagnosticsStatus => _diagnostics.Exists ? "A bounded local diagnostic log is available." : "No diagnostic log has been created.";
    public string DiagnosticsPath => _diagnostics.Path;
    public string LegacyImportStatus => _legacyImportStatus;
    public bool CanImportLegacy => _legacyDataPath is not null && !_isImportingLegacy && _main.CanModifyData;

    public UpdateExperienceState UpdateState => _updateState;
    public string UpdateStatus => _updateStatus;
    public string UpdateDetail => _updateDetail;
    public double UpdateProgress => _updateProgress;
    public bool IsChecking => _updateState == UpdateExperienceState.Checking;
    public bool IsDownloading => _updateState == UpdateExperienceState.Downloading;
    public bool CanCheckForUpdates => _updateState is UpdateExperienceState.Idle or UpdateExperienceState.Current or UpdateExperienceState.Failed;
    public bool CanDownloadUpdate => _updateState == UpdateExperienceState.Available && _release?.InstallerUri is not null && _release.InstallerSha256 is not null;
    public bool CanInstallUpdate => _updateState == UpdateExperienceState.Ready && _installerPath is not null;
    public bool CanOpenRelease => _release is not null && _updateState == UpdateExperienceState.Available;

    public void ReloadSchedule()
    {
        var settings = _main.CopySettings();
        _scheduleRemindersEnabled = settings.RemindersEnabled;
        _scheduleIntervalMinutes = settings.ReminderIntervalMinutes;
        _scheduleStart = FormatTime(settings.WorkdayStart);
        _scheduleEnd = FormatTime(settings.WorkdayEnd);
        _monday = settings.ReminderDays.Contains(DayOfWeek.Monday);
        _tuesday = settings.ReminderDays.Contains(DayOfWeek.Tuesday);
        _wednesday = settings.ReminderDays.Contains(DayOfWeek.Wednesday);
        _thursday = settings.ReminderDays.Contains(DayOfWeek.Thursday);
        _friday = settings.ReminderDays.Contains(DayOfWeek.Friday);
        _saturday = settings.ReminderDays.Contains(DayOfWeek.Saturday);
        _sunday = settings.ReminderDays.Contains(DayOfWeek.Sunday);
        _scheduleError = string.Empty;
        _scheduleNotice = string.Empty;
        _acceptedSchedule = ScheduleSignature();
        NotifySchedule();
    }

    public bool SaveSchedule()
    {
        if (!TryBuildSchedule(out var draft, out var error))
        {
            _scheduleError = error;
            _scheduleNotice = string.Empty;
            NotifySchedule();
            return false;
        }
        if (!_main.TryApplySchedule(draft, out error))
        {
            _scheduleError = error;
            _scheduleNotice = string.Empty;
            NotifySchedule();
            return false;
        }
        _scheduleError = string.Empty;
        _scheduleNotice = "Schedule saved on this PC.";
        _acceptedSchedule = ScheduleSignature();
        NotifySchedule();
        return true;
    }

    public void CancelSchedule() => ReloadSchedule();

    public void ReloadSettings()
    {
        var settings = _main.CopySettings();
        _goalText = settings.DailyGoalOz.ToString("0.#", CultureInfo.CurrentCulture);
        _soundsEnabled = settings.SoundsEnabled;
        _widgetMode = _main.CurrentWidgetMode is "compact" ? "compact" : "expanded";
        _settingsError = string.Empty;
        _settingsNotice = string.Empty;
        _acceptedSettings = SettingsSignature();
        NotifySettings();
    }

    public bool SaveSettings()
    {
        if (!double.TryParse(_goalText, NumberStyles.Number, CultureInfo.CurrentCulture, out var goal) ||
            !double.IsFinite(goal) || goal is < 8 or > 512)
        {
            _settingsError = "Enter a daily goal from 8 to 512 oz.";
            _settingsNotice = string.Empty;
            NotifySettings();
            return false;
        }
        if (_widgetMode is not "expanded" and not "compact")
        {
            _settingsError = "Choose an expanded or compact widget default.";
            _settingsNotice = string.Empty;
            NotifySettings();
            return false;
        }
        if (!_main.TryApplyPreferences(goal, _soundsEnabled, _widgetMode, out var error))
        {
            _settingsError = error;
            _settingsNotice = string.Empty;
            NotifySettings();
            return false;
        }
        _goalText = Math.Round(goal, 1).ToString("0.#", CultureInfo.CurrentCulture);
        _settingsError = string.Empty;
        _settingsNotice = "Settings saved on this PC.";
        _acceptedSettings = SettingsSignature();
        NotifySettings();
        return true;
    }

    public void CancelSettings() => ReloadSettings();

    public async Task InitializeUpdatesAsync(bool enabled)
    {
        if (_updateInitialized || !enabled) return;
        _updateInitialized = true;
        await CheckForUpdatesAsync();
    }

    public async Task CheckForUpdatesAsync()
    {
#if DEBUG
        SetUpdate(UpdateExperienceState.Unavailable, "Unavailable in development", "Update checks are disabled in development builds.");
        await Task.CompletedTask;
        return;
#else
        if (_updateState is UpdateExperienceState.Checking or UpdateExperienceState.Downloading or UpdateExperienceState.Installing) return;
        _updateCancellation?.Cancel();
        _updateCancellation?.Dispose();
        _updateCancellation = new CancellationTokenSource();
        SetUpdate(UpdateExperienceState.Checking, "Checking for updates…", "Contacting the official Waterline release endpoint.");
        try
        {
            _release = await _updates.CheckAsync(_updateCancellation.Token);
            if (_release is null || _release.Version <= _updates.CurrentVersion)
            {
                SetUpdate(UpdateExperienceState.Current, "Waterline is current", $"Installed version: {_updates.CurrentVersion.ToString(3)}.");
                return;
            }
            var detail = _release.InstallerUri is null
                ? $"Version {_release.Version} is published, but no trusted installer is attached."
                : $"Version {_release.Version} is ready to download from GitHub Releases.";
            SetUpdate(UpdateExperienceState.Available, "Update available", detail);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            _diagnostics.Record("update-check", exception);
            SetUpdate(UpdateExperienceState.Failed, "Could not check for updates", "Check your connection and try again. Hydration logging is unaffected.");
        }
#endif
    }

    public async Task DownloadUpdateAsync()
    {
        if (!CanDownloadUpdate || _release is null) return;
        _updateCancellation?.Cancel();
        _updateCancellation?.Dispose();
        _updateCancellation = new CancellationTokenSource();
        _updateProgress = 0;
        SetUpdate(UpdateExperienceState.Downloading, $"Downloading Waterline {_release.Version}", "Keep Waterline open while the installer downloads.");
        try
        {
            var progress = new Progress<double>(value =>
            {
                _updateProgress = Math.Clamp(value, 0, 100);
                OnPropertyChanged(nameof(UpdateProgress));
            });
            _installerPath = await _updates.DownloadAsync(_release, progress, _updateCancellation.Token);
            _updateProgress = 100;
            SetUpdate(UpdateExperienceState.Ready, "Ready to install", $"Waterline {_release.Version} is downloaded. Installation starts only when you choose Install.");
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            _diagnostics.Record("update-download", exception);
            SetUpdate(UpdateExperienceState.Failed, "Download failed", "The incomplete file was removed. Try the update check again.");
        }
    }

    public bool LaunchInstaller()
    {
        if (!CanInstallUpdate || _installerPath is null) return false;
        try
        {
            SetUpdate(UpdateExperienceState.Installing, "Starting installer…", "Waterline will close after the installer opens.");
            _updates.LaunchInstaller(_installerPath);
            return true;
        }
        catch (Exception exception)
        {
            _diagnostics.Record("update-install", exception);
            SetUpdate(UpdateExperienceState.Failed, "Could not start the installer", "The downloaded file was not launched. Check diagnostics and try again.");
            return false;
        }
    }

    public void OpenReleasePage()
    {
        if (!CanOpenRelease || _release is null) return;
        try { _updates.OpenReleasePage(_release.PageUrl); }
        catch (Exception exception)
        {
            _diagnostics.Record("release-page", exception);
            SetUpdate(UpdateExperienceState.Failed, "Could not open the release page", "Try checking for updates again.");
        }
    }

    public bool ClearDiagnostics()
    {
        var cleared = _diagnostics.Clear();
        _settingsNotice = cleared ? "Local diagnostics cleared." : "Waterline could not clear the diagnostic log.";
        _settingsError = cleared ? string.Empty : _settingsNotice;
        NotifySettings();
        OnPropertyChanged(nameof(DiagnosticsStatus));
        return cleared;
    }

    public bool ImportLegacyData()
    {
        if (!CanImportLegacy || _legacyDataPath is null) return false;
        _isImportingLegacy = true;
        OnPropertyChanged(nameof(CanImportLegacy));
        try
        {
            if (!_main.TryImportLegacyData(_legacyDataPath, out var merge, out var error))
            {
                _legacyImportStatus = error;
                _settingsError = error;
                NotifySettings();
                OnPropertyChanged(nameof(LegacyImportStatus));
                return false;
            }

            var details = $"Imported {merge.ImportedDrinks} drink entr{(merge.ImportedDrinks == 1 ? "y" : "ies")}" +
                          $", ignored {merge.DuplicateDrinks} duplicate{(merge.DuplicateDrinks == 1 ? string.Empty : "s")}" +
                          $", and skipped {merge.SkippedRecords} invalid record{(merge.SkippedRecords == 1 ? string.Empty : "s")}.";
            ReloadSchedule();
            ReloadSettings();
            _legacyImportStatus = merge.SettingsImported ? details + " Older settings were applied." : details;
            _settingsError = string.Empty;
            _settingsNotice = "Older Waterline data imported. A pre-import backup was preserved.";
            NotifySettings();
            OnPropertyChanged(nameof(LegacyImportStatus));
            return true;
        }
        finally
        {
            _isImportingLegacy = false;
            OnPropertyChanged(nameof(CanImportLegacy));
        }
    }

    public void PrepareSnapshot(string mode)
    {
        switch (mode)
        {
            case "schedule-dirty":
            case "unsaved":
                ScheduleIntervalMinutes = 45;
                Saturday = true;
                break;
            case "schedule-invalid":
                ScheduleStart = "5:00 PM";
                ScheduleEnd = "9:00 AM";
                SaveSchedule();
                break;
            case "settings-dirty":
                GoalText = "96";
                SoundsEnabled = false;
                break;
            case "update-checking":
                SetUpdate(UpdateExperienceState.Checking, "Checking for updates…", "Contacting the official Waterline release endpoint.");
                break;
            case "update-available":
                _release = new ReleaseInfo(new Version(2, 1, 0), "https://github.com/AayyKay/waterline/releases/tag/v2.1.0", new Uri("https://github.com/AayyKay/waterline/releases/download/v2.1.0/Waterline-Setup-2.1.0.exe"), new string('a', 64));
                SetUpdate(UpdateExperienceState.Available, "Update available", "Version 2.1.0 is ready to download from GitHub Releases.");
                break;
            case "update-downloading":
                _updateProgress = 58;
                SetUpdate(UpdateExperienceState.Downloading, "Downloading Waterline 2.1.0", "58% complete. Hydration logging remains available.");
                break;
            case "update-ready":
                _release = new ReleaseInfo(new Version(2, 1, 0), "https://github.com/AayyKay/waterline/releases/tag/v2.1.0", null, null);
                _installerPath = Path.Combine(Path.GetTempPath(), "Waterline-Setup-2.1.0-snapshot.exe");
                SetUpdate(UpdateExperienceState.Ready, "Ready to install", "Installation starts only when you choose Install.");
                break;
            case "update-failed":
                SetUpdate(UpdateExperienceState.Failed, "Could not check for updates", "Check your connection and try again. Hydration logging is unaffected.");
                break;
        }
    }

    private bool TryBuildSchedule(out WaterlineSettings draft, out string error)
    {
        draft = _main.CopySettings();
        if (!TryParseTime(_scheduleStart, out var start) || !TryParseTime(_scheduleEnd, out var end))
        {
            error = "Enter start and end times using your Windows time format.";
            return false;
        }
        if (start >= end)
        {
            error = "End time must be later than start time for a same-day schedule.";
            return false;
        }
        if (_scheduleIntervalMinutes is not (30 or 45 or 60 or 90 or 120))
        {
            error = "Choose a supported reminder interval.";
            return false;
        }
        draft.RemindersEnabled = _scheduleRemindersEnabled;
        draft.ReminderIntervalMinutes = _scheduleIntervalMinutes;
        draft.WorkdayStart = start;
        draft.WorkdayEnd = end;
        draft.ReminderDays = [];
        if (_monday) draft.ReminderDays.Add(DayOfWeek.Monday);
        if (_tuesday) draft.ReminderDays.Add(DayOfWeek.Tuesday);
        if (_wednesday) draft.ReminderDays.Add(DayOfWeek.Wednesday);
        if (_thursday) draft.ReminderDays.Add(DayOfWeek.Thursday);
        if (_friday) draft.ReminderDays.Add(DayOfWeek.Friday);
        if (_saturday) draft.ReminderDays.Add(DayOfWeek.Saturday);
        if (_sunday) draft.ReminderDays.Add(DayOfWeek.Sunday);
        error = string.Empty;
        return true;
    }

    private static bool TryParseTime(string value, out TimeSpan time)
    {
        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault, out var parsed))
        {
            time = parsed.TimeOfDay;
            return true;
        }
        time = default;
        return false;
    }

    private static string FormatTime(TimeSpan time) => DateTime.Today.Add(time).ToString("t", CultureInfo.CurrentCulture);

    private string ScheduleSignature() => string.Join('|', _scheduleRemindersEnabled, _scheduleIntervalMinutes, _scheduleStart.Trim(), _scheduleEnd.Trim(), _monday, _tuesday, _wednesday, _thursday, _friday, _saturday, _sunday);
    private string SettingsSignature() => string.Join('|', _goalText.Trim(), _soundsEnabled, _widgetMode);

    private void SetSchedule<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
        _scheduleError = string.Empty;
        _scheduleNotice = string.Empty;
        NotifySchedule();
    }

    private void SetSetting<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
        _settingsError = string.Empty;
        _settingsNotice = string.Empty;
        NotifySettings();
    }

    private void NotifySchedule()
    {
        OnPropertyChanged(nameof(ScheduleRemindersEnabled));
        OnPropertyChanged(nameof(ScheduleIntervalMinutes));
        OnPropertyChanged(nameof(ScheduleStart));
        OnPropertyChanged(nameof(ScheduleEnd));
        OnPropertyChanged(nameof(Monday)); OnPropertyChanged(nameof(Tuesday)); OnPropertyChanged(nameof(Wednesday)); OnPropertyChanged(nameof(Thursday)); OnPropertyChanged(nameof(Friday)); OnPropertyChanged(nameof(Saturday)); OnPropertyChanged(nameof(Sunday));
        OnPropertyChanged(nameof(HasScheduleChanges));
        OnPropertyChanged(nameof(ScheduleError));
        OnPropertyChanged(nameof(HasScheduleError));
        OnPropertyChanged(nameof(ScheduleNotice));
        OnPropertyChanged(nameof(HasScheduleNotice));
        OnPropertyChanged(nameof(SchedulePreview));
    }

    private void NotifySettings()
    {
        OnPropertyChanged(nameof(GoalText));
        OnPropertyChanged(nameof(SoundsEnabled));
        OnPropertyChanged(nameof(WidgetMode));
        OnPropertyChanged(nameof(HasSettingsChanges));
        OnPropertyChanged(nameof(SettingsError));
        OnPropertyChanged(nameof(HasSettingsError));
        OnPropertyChanged(nameof(SettingsNotice));
        OnPropertyChanged(nameof(HasSettingsNotice));
    }

    private void SetUpdate(UpdateExperienceState state, string status, string detail)
    {
        _updateState = state;
        _updateStatus = status;
        _updateDetail = detail;
        OnPropertyChanged(nameof(UpdateState));
        OnPropertyChanged(nameof(UpdateStatus));
        OnPropertyChanged(nameof(UpdateDetail));
        OnPropertyChanged(nameof(UpdateProgress));
        OnPropertyChanged(nameof(IsChecking));
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(CanCheckForUpdates));
        OnPropertyChanged(nameof(CanDownloadUpdate));
        OnPropertyChanged(nameof(CanInstallUpdate));
        OnPropertyChanged(nameof(CanOpenRelease));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _updateCancellation?.Cancel();
        _updateCancellation?.Dispose();
        _updates.Dispose();
    }
}
