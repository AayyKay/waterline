using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Waterline.Core;
using Waterline.Infrastructure;

namespace Waterline;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly AppStateStore _store;
    private readonly IClock _clockProvider;
    private TimeZoneInfo _timeZone;
    private readonly bool _usesSystemTimeZone;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _reminderTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly WaterlineState _state;
    private DateTimeOffset _now;
    private string _persistenceMessage = string.Empty;
    private string _activityMessage = string.Empty;
    private bool _activityIsError;
    private bool _isLoading;
    private bool _isSaving;
    private StateLoadStatus _loadStatus;
    private HistoryDayView? _selectedHistoryDay;

    public MainViewModel(AppStateStore store, IClock? clock = null, TimeZoneInfo? timeZone = null)
    {
        _store = store;
        _clockProvider = clock ?? new SystemClock();
        _usesSystemTimeZone = timeZone is null;
        _timeZone = timeZone ?? TimeZoneInfo.Local;
        _now = _clockProvider.Now;
        var load = store.Load();
        _state = load.State;
        _loadStatus = load.Status;
        _persistenceMessage = string.Join(" ", load.Messages);
        if (load.Status is StateLoadStatus.RecoveredFromBackup or StateLoadStatus.Unrecoverable)
        {
            _activityMessage = _persistenceMessage;
            _activityIsError = true;
        }
        Drinks = new ObservableCollection<DrinkEntry>(TodayEntries().OrderByDescending(d => d.RecordedAt));
        RecentEntries = new ObservableCollection<DrinkEntryView>();
        HistoryDays = new ObservableCollection<HistoryDayView>();
        HistoryEntries = new ObservableCollection<DrinkEntryView>();
        Configuration = new ConfigurationViewModel(this, new GitHubUpdateService(), new DiagnosticLog(store.FilePath));
        RefreshDrinkViews();
        RefreshHistory();
        _clock.Tick += (_, _) =>
        {
            RefreshFromSystemClock();
        };
        _reminderTimer.Tick += (_, _) => CheckReminder();
        _clock.Start();
        _reminderTimer.Start();
        RefreshAll();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<AppNotification>? NotificationRequested;
    public event EventHandler<string>? PersistenceIssue;
    public ObservableCollection<DrinkEntry> Drinks { get; }
    public ObservableCollection<DrinkEntryView> RecentEntries { get; }
    public ObservableCollection<HistoryDayView> HistoryDays { get; }
    public ObservableCollection<DrinkEntryView> HistoryEntries { get; }
    public ConfigurationViewModel Configuration { get; }
    public WaterlineSettings Settings => _state.Settings;
    public StateLoadStatus LoadStatus => _loadStatus;
    public string PersistenceMessage => _persistenceMessage;
    public bool HasPersistenceIssue => _loadStatus is StateLoadStatus.RecoveredFromBackup or StateLoadStatus.Unrecoverable || _activityIsError;
    public bool CanAcknowledgeRecovery => _loadStatus == StateLoadStatus.RecoveredFromBackup;
    public bool IsUnrecoverable => _loadStatus == StateLoadStatus.Unrecoverable;
    public string ActivityMessage => _activityMessage;
    public bool HasActivityMessage => !string.IsNullOrWhiteSpace(_activityMessage);
    public bool ActivityIsError => _activityIsError;
    public bool IsLoading => _isLoading;
    public bool IsSaving => _isSaving;
    public double TotalOz => CurrentProgress.TotalOz;
    public double RemainingOz => CurrentProgress.RemainingOz;
    public double ProgressPercent => Math.Min(100, CurrentProgress.Percent);
    public double ReservoirFillHeight => 266 * ProgressPercent / 100;
    public bool IsGoalComplete => CurrentProgress.IsComplete;
    public bool IsOverGoal => CurrentProgress.Percent > 100;
    public bool HasTodayEntries => Drinks.Count > 0;
    public bool CanModifyData => !_isLoading && _loadStatus is not StateLoadStatus.RecoveredFromBackup and not StateLoadStatus.Unrecoverable;
    public bool CanUndo => Drinks.Count > 0 && CanModifyData;
    public string ProgressLabel => $"{TotalOz:0.#} / {Settings.DailyGoalOz:0.#} oz";
    public string TotalLabel => $"{TotalOz:0.#}";
    public string GoalLabel => $"/ {Settings.DailyGoalOz:0.#} oz";
    public string PercentLabel => $"{CurrentProgress.Percent:0}% of your goal";
    public string RemainingLabel => IsOverGoal
        ? $"{TotalOz - Settings.DailyGoalOz:0.#} oz above goal"
        : RemainingOz > 0 ? $"{RemainingOz:0.#} oz to go" : "Goal complete";
    public string TodayStateLabel => IsLoading ? "LOADING LOCAL DATA" : IsSaving ? "SAVING LOCALLY" : IsOverGoal ? "GOAL EXCEEDED" : IsGoalComplete ? "GOAL COMPLETE" : "HYDRATION PROGRESS";
    public string DateLabel => TimeZoneInfo.ConvertTime(_now, _timeZone)
        .ToString("dddd, MMMM d")
        .ToUpperInvariant();
    public string ReminderLabel
    {
        get
        {
            if (!Settings.RemindersEnabled) return "Reminders are off";
            if (AreRemindersPaused) return "Reminders are paused";
            var plan = CurrentReminderPlan();
            return plan.DueAt is { } due
                ? $"Next reminder {TimeZoneInfo.ConvertTime(due, _timeZone):h:mm tt}"
                : "No more reminders today";
        }
    }
    public string PaceTitle
    {
        get
        {
            if (RemainingOz <= 0) return "Goal complete";
            var pace = PaceCalculator.Calculate(_now, Settings, TotalOz, _timeZone);
            return pace.Status switch
            {
                PaceStatus.Complete => "Goal complete",
                PaceStatus.NoPlan => "No plan today",
                PaceStatus.BeforeSchedule => $"Starts at {Settings.WorkdayStart.ToString(@"h\:mm")}",
                PaceStatus.AfterSchedule => $"{RemainingOz:0.#} oz left",
                PaceStatus.Behind => $"{Math.Abs(pace.DifferenceOz):0} oz behind",
                PaceStatus.Ahead => $"{pace.DifferenceOz:0} oz ahead",
                _ => "Right on schedule"
            };
        }
    }
    public string PaceBody
    {
        get
        {
            var pace = CurrentPace;
            return pace.Status switch
            {
                PaceStatus.Complete => "Today’s plan is complete. Further reminders are paused.",
                PaceStatus.NoPlan => "No hydration schedule applies today.",
                PaceStatus.BeforeSchedule => $"Your schedule begins at {FormatTime(Settings.WorkdayStart)}.",
                PaceStatus.AfterSchedule => $"The scheduled period ended with {pace.RemainingOz:0.#} oz remaining.",
                PaceStatus.Behind => $"About {Math.Abs(pace.DifferenceOz):0.#} oz behind the even plan. A gentle next amount is {pace.SuggestedNextOz:0.#} oz.",
                PaceStatus.Ahead => $"About {pace.DifferenceOz:0.#} oz ahead of the even plan. Keep a comfortable pace.",
                _ => $"Right near the even target of {pace.TargetOz:0.#} oz by now."
            };
        }
    }
    public string PaceGuidance => "Pace is general guidance, not medical advice.";
    public double PaceTargetPercent => Settings.DailyGoalOz <= 0 ? 0 : Math.Clamp(CurrentPace.TargetOz / Settings.DailyGoalOz * 100, 0, 100);
    public IReadOnlyList<DailyTotal> WeeklyTotals => HydrationCalculator
        .GetRecentDays(_state.Drinks, LocalDay(_now), 7, Settings.DailyGoalOz, _timeZone)
        .Select(day =>
        {
            var label = day.Day == LocalDay(_now) ? "Today" : day.Day.ToDateTime(TimeOnly.MinValue).ToString("ddd")[..1];
            var height = day.TotalOz <= 0 ? 0 : Math.Clamp(day.TotalOz / Math.Max(1, Settings.DailyGoalOz) * 105, 8, 105);
            return new DailyTotal(label, day.TotalOz, height);
        })
        .ToList();

    public HistoryDayView? SelectedHistoryDay
    {
        get => _selectedHistoryDay;
        set
        {
            if (ReferenceEquals(_selectedHistoryDay, value)) return;
            _selectedHistoryDay = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedHistoryTitle));
            OnPropertyChanged(nameof(SelectedHistorySummary));
            RefreshSelectedHistoryEntries();
        }
    }
    public string SelectedHistoryTitle => _selectedHistoryDay is null
        ? "Select a day"
        : _selectedHistoryDay.Day == LocalDay(_now) ? "Today’s entries" : $"{_selectedHistoryDay.DayName} entries";
    public string SelectedHistorySummary => _selectedHistoryDay is null
        ? "Choose a day to see its details."
        : $"{_selectedHistoryDay.TotalLabel} · {_selectedHistoryDay.StatusLabel}";
    public bool HasSelectedHistoryEntries => HistoryEntries.Count > 0;
    public string StateFilePath => _store.FilePath;
    public string RecoveryStatusLabel => _loadStatus switch
    {
        StateLoadStatus.RecoveredFromBackup => "Recovered backup loaded · saving requires confirmation",
        StateLoadStatus.Unrecoverable => "Local data needs attention · existing files are unchanged",
        StateLoadStatus.Migrated => "Legacy Waterline data loaded safely",
        _ => "Local data is healthy"
    };

    public WaterlineSettings CopySettings() => new()
    {
        DailyGoalOz = Settings.DailyGoalOz,
        ReminderIntervalMinutes = Settings.ReminderIntervalMinutes,
        WorkdayStart = Settings.WorkdayStart,
        WorkdayEnd = Settings.WorkdayEnd,
        RemindersEnabled = Settings.RemindersEnabled,
        SoundsEnabled = Settings.SoundsEnabled,
        ReminderDays = [.. Settings.ReminderDays]
    };

    public string CurrentWidgetMode => _state.Desktop.WidgetMode;
    public bool AreRemindersPaused => Settings.RemindersEnabled && _state.Runtime.RemindersPaused;
    public bool CanToggleReminderPause => Settings.RemindersEnabled && !IsGoalComplete;
    public string ReminderPauseActionLabel => AreRemindersPaused ? "Resume reminders" : "Pause reminders";

    public WidgetPlacement? CopyWidgetPlacement() => _state.Desktop.WidgetPlacement is not { } placement
        ? null
        : new WidgetPlacement
        {
            MonitorId = placement.MonitorId,
            AnchorX = placement.AnchorX,
            AnchorY = placement.AnchorY,
            Width = placement.Width,
            Height = placement.Height,
            DpiScale = placement.DpiScale
        };

    public string PreviewReminder(WaterlineSettings draft)
    {
        var plan = ReminderScheduler.GetPlan(
            _now, draft, TotalOz, TodayEntries().MaxBy(entry => entry.RecordedAt)?.RecordedAt,
            _state.Runtime.LastNotificationAt, _timeZone);
        if (!draft.RemindersEnabled) return "Reminders are off. The schedule will be saved without notifications.";
        if (plan.DueAt is not { } due) return "No reminder is eligible in the current schedule.";
        var local = TimeZoneInfo.ConvertTime(due, _timeZone);
        return plan.IsActive
            ? $"Next eligible reminder: {local:dddd} at {local:t}."
            : $"Schedule resumes {local:dddd} at {local:t}.";
    }

    public bool TryApplySchedule(WaterlineSettings draft, out string error)
    {
        var previous = CopySettings();
        var previousPaused = _state.Runtime.RemindersPaused;
        Settings.RemindersEnabled = draft.RemindersEnabled;
        Settings.ReminderIntervalMinutes = draft.ReminderIntervalMinutes;
        Settings.WorkdayStart = draft.WorkdayStart;
        Settings.WorkdayEnd = draft.WorkdayEnd;
        Settings.ReminderDays = [.. draft.ReminderDays];
        if (!draft.RemindersEnabled) _state.Runtime.RemindersPaused = false;
        if (!Persist())
        {
            RestoreSettings(previous);
            _state.Runtime.RemindersPaused = previousPaused;
            error = PersistenceMessage;
            RefreshAll();
            return false;
        }
        error = string.Empty;
        SetActivityMessage("Reminder schedule saved locally.", false);
        RefreshAll();
        return true;
    }

    public bool TrySaveWidgetLayout(string mode, WidgetPlacement placement)
    {
        if (mode is not "expanded" and not "compact") return false;
        if (StateValidator.Validate(new WaterlineState
            {
                Settings = CopySettings(),
                Desktop = new DesktopState { WidgetMode = mode, WidgetPlacement = placement }
            }).Any(error => error.Contains("Widget", StringComparison.OrdinalIgnoreCase))) return false;

        var previousMode = _state.Desktop.WidgetMode;
        var previousPlacement = CopyWidgetPlacement();
        _state.Desktop.WidgetMode = mode;
        _state.Desktop.WidgetPlacement = placement;
        if (Persist())
        {
            OnPropertyChanged(nameof(CurrentWidgetMode));
            return true;
        }
        _state.Desktop.WidgetMode = previousMode;
        _state.Desktop.WidgetPlacement = previousPlacement;
        OnPropertyChanged(nameof(CurrentWidgetMode));
        return false;
    }

    public bool SetRemindersPaused(bool paused)
    {
        if (!Settings.RemindersEnabled || IsGoalComplete) return false;
        var previous = _state.Runtime.RemindersPaused;
        _state.Runtime.RemindersPaused = paused;
        if (!Persist())
        {
            _state.Runtime.RemindersPaused = previous;
            RefreshAll();
            return false;
        }
        SetActivityMessage(paused ? "Reminders paused." : "Reminders resumed.", false);
        RefreshAll();
        return true;
    }

    public bool TryApplyPreferences(double dailyGoalOz, bool soundsEnabled, string widgetMode, out string error)
    {
        var previous = CopySettings();
        var previousMode = _state.Desktop.WidgetMode;
        Settings.DailyGoalOz = Math.Round(dailyGoalOz, 1);
        Settings.SoundsEnabled = soundsEnabled;
        _state.Desktop.WidgetMode = widgetMode;
        if (!Persist())
        {
            RestoreSettings(previous);
            _state.Desktop.WidgetMode = previousMode;
            error = PersistenceMessage;
            RefreshAll();
            return false;
        }
        error = string.Empty;
        SetActivityMessage("Settings saved locally.", false);
        RefreshHistory();
        RefreshAll();
        return true;
    }

    private void RestoreSettings(WaterlineSettings previous)
    {
        Settings.DailyGoalOz = previous.DailyGoalOz;
        Settings.ReminderIntervalMinutes = previous.ReminderIntervalMinutes;
        Settings.WorkdayStart = previous.WorkdayStart;
        Settings.WorkdayEnd = previous.WorkdayEnd;
        Settings.RemindersEnabled = previous.RemindersEnabled;
        Settings.SoundsEnabled = previous.SoundsEnabled;
        Settings.ReminderDays = [.. previous.ReminderDays];
    }

    public void AddDrink(double amountOz)
    {
        if (!CanModifyData || !StateValidator.IsValidAmount(amountOz)) return;
        var entry = new DrinkEntry { AmountOz = Math.Round(amountOz, 1), RecordedAt = _clockProvider.Now };
        var previousNotification = _state.Runtime.LastNotificationAt;
        _state.Drinks.Add(entry);
        _state.Runtime.LastNotificationAt = null;
        if (!Persist())
        {
            _state.Drinks.Remove(entry);
            _state.Runtime.LastNotificationAt = previousNotification;
            return;
        }
        Drinks.Insert(0, entry);
        SetActivityMessage($"Added {entry.AmountOz:0.#} oz to today.", false);
        RefreshDrinkViews();
        RefreshHistory();
        RefreshAll();
        if (Settings.SoundsEnabled) SoundService.PlayLog();
    }

    public void UndoLastDrink()
    {
        if (!CanModifyData) return;
        var last = TodayEntries().MaxBy(d => d.RecordedAt);
        if (last is null) return;
        _state.Drinks.Remove(last);
        if (!Persist())
        {
            _state.Drinks.Add(last);
            return;
        }
        Drinks.Remove(last);
        SetActivityMessage($"Removed {last.AmountOz:0.#} oz from today.", false);
        RefreshDrinkViews();
        RefreshHistory();
        RefreshAll();
    }

    public void AcknowledgeRecovery()
    {
        if (!CanAcknowledgeRecovery) return;
        if (!_store.AcknowledgeRecoveredState())
        {
            SetActivityMessage("Waterline could not preserve the damaged state file. Saving remains blocked.", true);
            return;
        }
        _loadStatus = StateLoadStatus.Loaded;
        OnPropertyChanged(nameof(LoadStatus));
        OnPropertyChanged(nameof(CanAcknowledgeRecovery));
        OnPropertyChanged(nameof(CanModifyData));
        if (Persist()) SetActivityMessage("Recovered data is active and saving is available again.", false);
        RefreshAll();
    }

    public void SaveSettings()
    {
        Persist();
        RefreshHistory();
        RefreshAll();
    }

    public void ToggleDay(DayOfWeek day, bool enabled)
    {
        if (enabled) Settings.ReminderDays.Add(day); else Settings.ReminderDays.Remove(day);
        SaveSettings();
    }

    private HydrationProgress CurrentProgress =>
        HydrationCalculator.GetProgress(_state.Drinks, LocalDay(_now), Settings.DailyGoalOz, _timeZone);
    private PaceResult CurrentPace => PaceCalculator.Calculate(_now, Settings, TotalOz, _timeZone);

    private DateOnly LocalDay(DateTimeOffset instant) => HydrationCalculator.GetLocalDay(instant, _timeZone);

    private IReadOnlyList<DrinkEntry> TodayEntries() =>
        HydrationCalculator.GetEntriesForDay(_state.Drinks, LocalDay(_now), _timeZone);

    private ReminderPlan CurrentReminderPlan() => AreRemindersPaused
        ? new ReminderPlan(null, false)
        : ReminderScheduler.GetPlan(
            _now, Settings, TotalOz, TodayEntries().MaxBy(d => d.RecordedAt)?.RecordedAt,
            _state.Runtime.LastNotificationAt, _timeZone);

    private void CheckReminder()
    {
        if (AreRemindersPaused) return;
        var plan = CurrentReminderPlan();
        if (plan.IsActive && plan.DueAt is { } due && due <= _now)
        {
            var previousNotification = _state.Runtime.LastNotificationAt;
            _state.Runtime.LastNotificationAt = _now;
            if (!Persist())
            {
                _state.Runtime.LastNotificationAt = previousNotification;
                return;
            }
            if (Settings.SoundsEnabled) SoundService.PlayReminder();
            NotificationRequested?.Invoke(this, new AppNotification("Time for a small water break", $"{RemainingOz:0.#} oz left today. Take a sip, then log it in Waterline."));
        }
        RefreshAll();
    }

    private bool Persist()
    {
        _isSaving = true;
        OnPropertyChanged(nameof(IsSaving));
        OnPropertyChanged(nameof(TodayStateLabel));
        var result = _store.Save(_state);
        _isSaving = false;
        OnPropertyChanged(nameof(IsSaving));
        OnPropertyChanged(nameof(TodayStateLabel));
        if (result.Success)
        {
            SetPersistenceMessage(string.Empty);
            return true;
        }
        SetPersistenceMessage(result.Message ?? "Waterline could not save local state.");
        SetActivityMessage(_persistenceMessage, true);
        PersistenceIssue?.Invoke(this, _persistenceMessage);
        return false;
    }

    private void SetPersistenceMessage(string message)
    {
        if (_persistenceMessage == message) return;
        _persistenceMessage = message;
        OnPropertyChanged(nameof(PersistenceMessage));
        OnPropertyChanged(nameof(HasPersistenceIssue));
        OnPropertyChanged(nameof(RecoveryStatusLabel));
    }

    private void SetActivityMessage(string message, bool isError)
    {
        _activityMessage = message;
        _activityIsError = isError;
        OnPropertyChanged(nameof(ActivityMessage));
        OnPropertyChanged(nameof(HasActivityMessage));
        OnPropertyChanged(nameof(ActivityIsError));
        OnPropertyChanged(nameof(HasPersistenceIssue));
    }

    private void RefreshDrinkCollection()
    {
        Drinks.Clear();
        foreach (var entry in TodayEntries().OrderByDescending(entry => entry.RecordedAt))
            Drinks.Add(entry);
        RefreshDrinkViews();
    }

    private void RefreshDrinkViews()
    {
        RecentEntries.Clear();
        foreach (var entry in TodayEntries().OrderByDescending(entry => entry.RecordedAt))
            RecentEntries.Add(ToView(entry));
    }

    private void RefreshHistory()
    {
        var selectedDay = _selectedHistoryDay?.Day ?? LocalDay(_now);
        HistoryDays.Clear();
        foreach (var day in HydrationCalculator.GetRecentDays(_state.Drinks, LocalDay(_now), 7, Settings.DailyGoalOz, _timeZone).Reverse())
        {
            var percent = Settings.DailyGoalOz <= 0 ? 0 : Math.Max(0, day.TotalOz / Settings.DailyGoalOz * 100);
            HistoryDays.Add(new HistoryDayView(
                day.Day,
                day.Day == LocalDay(_now) ? "Today" : day.Day.ToDateTime(TimeOnly.MinValue).ToString("dddd"),
                day.Day.ToDateTime(TimeOnly.MinValue).ToString("MMM d"),
                $"{day.TotalOz:0.#} oz",
                Math.Min(100, percent),
                day.GoalReached,
                percent > 100,
                percent > 100 ? $"{percent:0}% · above goal" : day.GoalReached ? "Goal complete" : day.TotalOz > 0 ? $"{percent:0}% of goal" : "No water logged"));
        }
        SelectedHistoryDay = HistoryDays.FirstOrDefault(day => day.Day == selectedDay) ?? HistoryDays.FirstOrDefault();
        RefreshSelectedHistoryEntries();
    }

    private void RefreshSelectedHistoryEntries()
    {
        HistoryEntries.Clear();
        if (_selectedHistoryDay is not null)
        {
            foreach (var entry in _state.Drinks
                         .Where(entry => HydrationCalculator.GetLocalDay(entry.RecordedAt, _timeZone) == _selectedHistoryDay.Day)
                         .OrderByDescending(entry => entry.RecordedAt))
                HistoryEntries.Add(ToView(entry));
        }
        OnPropertyChanged(nameof(HasSelectedHistoryEntries));
    }

    private DrinkEntryView ToView(DrinkEntry entry)
    {
        var local = TimeZoneInfo.ConvertTime(entry.RecordedAt, _timeZone);
        var amount = $"{entry.AmountOz:0.#} oz";
        var time = local.ToString("h:mm tt");
        return new DrinkEntryView(entry.Id, amount, time, $"{amount} logged at {time}");
    }

    private static string FormatTime(TimeSpan time) => DateTime.Today.Add(time).ToString("t");

    public void RefreshFromSystemClock()
    {
        var previousDay = LocalDay(_now);
        var previousZoneId = _timeZone.Id;
        if (_usesSystemTimeZone)
        {
            TimeZoneInfo.ClearCachedData();
            _timeZone = TimeZoneInfo.Local;
        }
        _now = _clockProvider.Now;
        if (LocalDay(_now) != previousDay || _timeZone.Id != previousZoneId)
        {
            RefreshDrinkCollection();
            RefreshHistory();
        }
        RefreshAll();
    }

    public void RefreshAfterSystemResume()
    {
        RefreshFromSystemClock();
        CheckReminder();
    }

    public void PrepareSnapshotFixture(string mode)
    {
        if (mode is "empty" or "dialog" or "dialog-invalid") return;
        var today = LocalDay(_now);
        _state.Drinks.Clear();
        if (mode == "loading")
        {
            _isLoading = true;
            SetActivityMessage("Loading your local Waterline data…", false);
            RefreshDrinkCollection();
            RefreshHistory();
            RefreshAll();
            return;
        }
        AddFixture(today.AddDays(-5), 28, 9, 10);
        AddFixture(today.AddDays(-4), 52, 11, 20);
        AddFixture(today.AddDays(-3), 80, 15, 5);
        AddFixture(today.AddDays(-2), 92, 16, 15);
        AddFixture(today.AddDays(-1), 44, 13, 35);
        if (mode == "goal")
        {
            AddFixture(today, 64, 8, 30);
            AddFixture(today, 16, 11, 45);
        }
        else if (mode == "over-goal")
        {
            AddFixture(today, 64, 8, 30);
            AddFixture(today, 28, 11, 45);
        }
        else
        {
            AddFixture(today, 12, 7, 3);
            AddFixture(today, 8, 8, 27);
            AddFixture(today, 16, 9, 14);
        }
        if (mode == "save-failed") SetActivityMessage("Waterline could not save this change. Your previous total is unchanged.", true);
        if (mode == "recovery")
        {
            _loadStatus = StateLoadStatus.RecoveredFromBackup;
            _persistenceMessage = "Waterline recovered the last valid backup. Review it before restoring saving.";
            SetActivityMessage(_persistenceMessage, true);
        }
        if (mode == "unrecoverable")
        {
            _state.Drinks.Clear();
            _loadStatus = StateLoadStatus.Unrecoverable;
            _persistenceMessage = "No valid Waterline state could be loaded. Existing files were left unchanged.";
            SetActivityMessage(_persistenceMessage, true);
        }
        RefreshDrinkCollection();
        RefreshHistory();
        RefreshAll();
    }

    private void AddFixture(DateOnly day, double amount, int hour, int minute)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified);
        _state.Drinks.Add(new DrinkEntry
        {
            Id = $"snapshot-{day:yyyyMMdd}-{hour:00}{minute:00}-{amount:0.#}",
            AmountOz = amount,
            RecordedAt = new DateTimeOffset(local, _timeZone.GetUtcOffset(local))
        });
    }

    private void RefreshAll()
    {
        OnPropertyChanged(nameof(TotalOz));
        OnPropertyChanged(nameof(RemainingOz));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(ReservoirFillHeight));
        OnPropertyChanged(nameof(IsGoalComplete));
        OnPropertyChanged(nameof(IsOverGoal));
        OnPropertyChanged(nameof(HasTodayEntries));
        OnPropertyChanged(nameof(CanModifyData));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(LoadStatus));
        OnPropertyChanged(nameof(CanAcknowledgeRecovery));
        OnPropertyChanged(nameof(IsUnrecoverable));
        OnPropertyChanged(nameof(HasPersistenceIssue));
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(TotalLabel));
        OnPropertyChanged(nameof(GoalLabel));
        OnPropertyChanged(nameof(PercentLabel));
        OnPropertyChanged(nameof(RemainingLabel));
        OnPropertyChanged(nameof(TodayStateLabel));
        OnPropertyChanged(nameof(DateLabel));
        OnPropertyChanged(nameof(ReminderLabel));
        OnPropertyChanged(nameof(AreRemindersPaused));
        OnPropertyChanged(nameof(CanToggleReminderPause));
        OnPropertyChanged(nameof(ReminderPauseActionLabel));
        OnPropertyChanged(nameof(CurrentWidgetMode));
        OnPropertyChanged(nameof(PaceTitle));
        OnPropertyChanged(nameof(PaceBody));
        OnPropertyChanged(nameof(PaceGuidance));
        OnPropertyChanged(nameof(PaceTargetPercent));
        OnPropertyChanged(nameof(WeeklyTotals));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void Dispose()
    {
        _clock.Stop();
        _reminderTimer.Stop();
        Configuration.Dispose();
    }
}
