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
    private readonly TimeZoneInfo _timeZone;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _reminderTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly WaterlineState _state;
    private DateTimeOffset _now;
    private string _persistenceMessage = string.Empty;

    public MainViewModel(AppStateStore store, IClock? clock = null, TimeZoneInfo? timeZone = null)
    {
        _store = store;
        _clockProvider = clock ?? new SystemClock();
        _timeZone = timeZone ?? TimeZoneInfo.Local;
        _now = _clockProvider.Now;
        var load = store.Load();
        _state = load.State;
        LoadStatus = load.Status;
        _persistenceMessage = string.Join(" ", load.Messages);
        Drinks = new ObservableCollection<DrinkEntry>(TodayEntries().OrderByDescending(d => d.RecordedAt));
        _clock.Tick += (_, _) =>
        {
            var previousDay = LocalDay(_now);
            _now = _clockProvider.Now;
            if (LocalDay(_now) != previousDay) RefreshDrinkCollection();
            RefreshAll();
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
    public WaterlineSettings Settings => _state.Settings;
    public StateLoadStatus LoadStatus { get; }
    public string PersistenceMessage => _persistenceMessage;
    public bool HasPersistenceIssue => !string.IsNullOrWhiteSpace(_persistenceMessage);
    public double TotalOz => CurrentProgress.TotalOz;
    public double RemainingOz => CurrentProgress.RemainingOz;
    public double ProgressPercent => Math.Min(100, CurrentProgress.Percent);
    public string ProgressLabel => $"{TotalOz:0.#} / {Settings.DailyGoalOz:0.#} oz";
    public string PercentLabel => $"{ProgressPercent:0}% of your goal";
    public string RemainingLabel => RemainingOz > 0 ? $"{RemainingOz:0.#} oz to go" : "Goal complete!";
    public string DateLabel => TimeZoneInfo.ConvertTime(_now, _timeZone)
        .ToString("dddd, MMMM d")
        .ToUpperInvariant();
    public string ReminderLabel
    {
        get
        {
            if (!Settings.RemindersEnabled) return "Reminders are off";
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
    public IReadOnlyList<DailyTotal> WeeklyTotals => HydrationCalculator
        .GetRecentDays(_state.Drinks, LocalDay(_now), 7, Settings.DailyGoalOz, _timeZone)
        .Select(day =>
        {
            var label = day.Day == LocalDay(_now) ? "Today" : day.Day.ToDateTime(TimeOnly.MinValue).ToString("ddd")[..1];
            var height = day.TotalOz <= 0 ? 0 : Math.Clamp(day.TotalOz / Math.Max(1, Settings.DailyGoalOz) * 105, 8, 105);
            return new DailyTotal(label, day.TotalOz, height);
        })
        .ToList();

    public void AddDrink(double amountOz)
    {
        if (!StateValidator.IsValidAmount(amountOz)) return;
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
        RefreshAll();
        if (Settings.SoundsEnabled) SoundService.PlayLog();
    }

    public void UndoLastDrink()
    {
        var last = TodayEntries().MaxBy(d => d.RecordedAt);
        if (last is null) return;
        _state.Drinks.Remove(last);
        if (!Persist())
        {
            _state.Drinks.Add(last);
            return;
        }
        Drinks.Remove(last);
        RefreshAll();
    }

    public void SaveSettings()
    {
        Persist();
        RefreshAll();
    }

    public void ToggleDay(DayOfWeek day, bool enabled)
    {
        if (enabled) Settings.ReminderDays.Add(day); else Settings.ReminderDays.Remove(day);
        SaveSettings();
    }

    private HydrationProgress CurrentProgress =>
        HydrationCalculator.GetProgress(_state.Drinks, LocalDay(_now), Settings.DailyGoalOz, _timeZone);

    private DateOnly LocalDay(DateTimeOffset instant) => HydrationCalculator.GetLocalDay(instant, _timeZone);

    private IEnumerable<DrinkEntry> TodayEntries() =>
        _state.Drinks.Where(d => HydrationCalculator.GetLocalDay(d.RecordedAt, _timeZone) == LocalDay(_now));

    private ReminderPlan CurrentReminderPlan() => ReminderScheduler.GetPlan(
        _now, Settings, TotalOz, TodayEntries().MaxBy(d => d.RecordedAt)?.RecordedAt,
        _state.Runtime.LastNotificationAt, _timeZone);

    private void CheckReminder()
    {
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
        var result = _store.Save(_state);
        if (result.Success)
        {
            SetPersistenceMessage(string.Empty);
            return true;
        }
        SetPersistenceMessage(result.Message ?? "Waterline could not save local state.");
        PersistenceIssue?.Invoke(this, _persistenceMessage);
        return false;
    }

    private void SetPersistenceMessage(string message)
    {
        if (_persistenceMessage == message) return;
        _persistenceMessage = message;
        OnPropertyChanged(nameof(PersistenceMessage));
        OnPropertyChanged(nameof(HasPersistenceIssue));
    }

    private void RefreshDrinkCollection()
    {
        Drinks.Clear();
        foreach (var entry in TodayEntries().OrderByDescending(entry => entry.RecordedAt))
            Drinks.Add(entry);
    }

    private void RefreshAll()
    {
        OnPropertyChanged(nameof(TotalOz));
        OnPropertyChanged(nameof(RemainingOz));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(PercentLabel));
        OnPropertyChanged(nameof(RemainingLabel));
        OnPropertyChanged(nameof(DateLabel));
        OnPropertyChanged(nameof(ReminderLabel));
        OnPropertyChanged(nameof(PaceTitle));
        OnPropertyChanged(nameof(WeeklyTotals));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void Dispose()
    {
        _clock.Stop();
        _reminderTimer.Stop();
    }
}
