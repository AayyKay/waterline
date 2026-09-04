using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Media;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace Waterline;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly AppStateStore _store;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _reminderTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly WaterlineState _state;
    private DateTimeOffset _now = DateTimeOffset.Now;

    public MainViewModel(AppStateStore store)
    {
        _store = store;
        _state = store.Load();
        _state.Drinks ??= [];
        _state.Settings ??= new WaterlineSettings();
        Drinks = new ObservableCollection<DrinkEntry>(TodayEntries().OrderByDescending(d => d.At));
        _clock.Tick += (_, _) => { _now = DateTimeOffset.Now; RefreshAll(); };
        _reminderTimer.Tick += (_, _) => CheckReminder();
        _clock.Start();
        _reminderTimer.Start();
        RefreshAll();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<AppNotification>? NotificationRequested;
    public ObservableCollection<DrinkEntry> Drinks { get; }
    public WaterlineSettings Settings => _state.Settings;
    public double TotalOz => TodayEntries().Sum(d => d.AmountOz);
    public double RemainingOz => Math.Max(0, Settings.DailyGoalOz - TotalOz);
    public double ProgressPercent => Settings.DailyGoalOz <= 0 ? 0 : Math.Min(100, TotalOz / Settings.DailyGoalOz * 100);
    public string ProgressLabel => $"{TotalOz:0.#} / {Settings.DailyGoalOz:0.#} oz";
    public string PercentLabel => $"{ProgressPercent:0}% of your goal";
    public string RemainingLabel => RemainingOz > 0 ? $"{RemainingOz:0.#} oz to go" : "Goal complete!";
    public string DateLabel => _now.ToString("dddd, MMMM d").ToUpperInvariant();
    public string ReminderLabel
    {
        get
        {
            if (!Settings.RemindersEnabled) return "Reminders are off";
            var plan = CurrentReminderPlan();
            return plan.DueAt is { } due ? $"Next reminder {due.LocalDateTime:h:mm tt}" : "No more reminders today";
        }
    }
    public string PaceTitle
    {
        get
        {
            if (RemainingOz <= 0) return "Goal complete";
            var local = _now.LocalDateTime;
            if (!Settings.ReminderDays.Contains(local.DayOfWeek)) return "No plan today";
            var start = local.Date + Settings.WorkdayStart;
            var end = local.Date + Settings.WorkdayEnd;
            if (local < start) return $"Starts at {start:h:mm tt}";
            if (local > end) return $"{RemainingOz:0.#} oz left";
            var target = Settings.DailyGoalOz * Math.Clamp((local - start).TotalMinutes / Math.Max(1, (end - start).TotalMinutes), 0, 1);
            var difference = TotalOz - target;
            var tolerance = Math.Max(4, Settings.DailyGoalOz * .05);
            if (difference < -tolerance) return $"{Math.Abs(difference):0} oz behind";
            if (difference > tolerance) return $"{difference:0} oz ahead";
            return "Right on schedule";
        }
    }
    public IReadOnlyList<DailyTotal> WeeklyTotals => Enumerable.Range(0, 7)
        .Select(offset => DateOnly.FromDateTime(_now.LocalDateTime.Date.AddDays(offset - 6)))
        .Select(day => new DailyTotal(day, _state.Drinks.Where(d => DateOnly.FromDateTime(d.At.LocalDateTime) == day).Sum(d => d.AmountOz)))
        .ToList();

    public void AddDrink(double amountOz)
    {
        if (amountOz is <= 0 or > 64) return;
        var entry = new DrinkEntry { AmountOz = Math.Round(amountOz, 1), At = DateTimeOffset.Now };
        _state.Drinks.Add(entry);
        Drinks.Insert(0, entry);
        SaveAndRefresh();
        if (Settings.SoundsEnabled) SystemSounds.Asterisk.Play();
    }

    public void UndoLastDrink()
    {
        var last = TodayEntries().MaxBy(d => d.At);
        if (last is null) return;
        _state.Drinks.Remove(last);
        Drinks.Remove(last);
        SaveAndRefresh();
    }

    public void SaveSettings()
    {
        _store.Save(_state);
        RefreshAll();
    }

    public void ToggleDay(DayOfWeek day, bool enabled)
    {
        if (enabled) Settings.ReminderDays.Add(day); else Settings.ReminderDays.Remove(day);
        SaveSettings();
    }

    private IEnumerable<DrinkEntry> TodayEntries() =>
        _state.Drinks.Where(d => d.At.LocalDateTime.Date == _now.LocalDateTime.Date);

    private ReminderPlan CurrentReminderPlan() => ReminderScheduler.GetPlan(
        _now, Settings, TotalOz, TodayEntries().MaxBy(d => d.At)?.At, _state.LastNotificationAt);

    private void CheckReminder()
    {
        var plan = CurrentReminderPlan();
        if (plan.IsActive && plan.DueAt is { } due && due <= _now)
        {
            _state.LastNotificationAt = _now;
            _store.Save(_state);
            if (Settings.SoundsEnabled) SystemSounds.Exclamation.Play();
            NotificationRequested?.Invoke(this, new AppNotification("Time for a small water break", $"{RemainingOz:0.#} oz left today. Take a sip, then log it in Waterline."));
        }
        RefreshAll();
    }

    private void SaveAndRefresh()
    {
        _state.LastNotificationAt = null;
        _store.Save(_state);
        RefreshAll();
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
