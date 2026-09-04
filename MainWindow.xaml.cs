using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Waterline;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly GitHubUpdateService _updates = new();
    private ReleaseInfo? _availableRelease;
    private int _selectedInterval;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        LoadSettingsControls();
        Loaded += async (_, _) => await CheckForUpdatesAsync(false);
    }

    private void LoadSettingsControls()
    {
        var settings = _viewModel.Settings;
        _selectedInterval = settings.ReminderIntervalMinutes;
        GoalBox.Text = settings.DailyGoalOz.ToString("0.#", CultureInfo.CurrentCulture);
        StartBox.Text = settings.WorkdayStart.ToString(@"hh\:mm");
        EndBox.Text = settings.WorkdayEnd.ToString(@"hh\:mm");
        RemindersBox.IsChecked = settings.RemindersEnabled;
        SoundsBox.IsChecked = settings.SoundsEnabled;
        foreach (var toggle in IntervalButtons()) toggle.IsChecked = int.Parse(toggle.Tag.ToString()!) == _selectedInterval;
        foreach (var pair in DayButtons()) pair.Value.IsChecked = settings.ReminderDays.Contains(pair.Key);
    }

    private IReadOnlyList<ToggleButton> IntervalButtons() => [Interval30, Interval45, Interval60, Interval90, Interval120];
    private Dictionary<DayOfWeek, ToggleButton> DayButtons() => new()
    {
        [DayOfWeek.Sunday] = SunBox, [DayOfWeek.Monday] = MonBox, [DayOfWeek.Tuesday] = TueBox,
        [DayOfWeek.Wednesday] = WedBox, [DayOfWeek.Thursday] = ThuBox,
        [DayOfWeek.Friday] = FriBox, [DayOfWeek.Saturday] = SatBox
    };

    private void Add8_Click(object sender, RoutedEventArgs e) => _viewModel.AddDrink(8);
    private void Add12_Click(object sender, RoutedEventArgs e) => _viewModel.AddDrink(12);
    private void Add16_Click(object sender, RoutedEventArgs e) => _viewModel.AddDrink(16);
    private void Undo_Click(object sender, RoutedEventArgs e) => _viewModel.UndoLastDrink();
    private void Widget_Click(object sender, RoutedEventArgs e) => WidgetWindow.ShowOrActivate(_viewModel);
    private void Today_Click(object sender, RoutedEventArgs e) => MainScroll.ScrollToTop();
    private void Insights_Click(object sender, RoutedEventArgs e) => InsightsSection.BringIntoView();
    private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings(false);
    private void Schedule_Click(object sender, RoutedEventArgs e) => ShowSettings(true);
    public void ShowSettingsForSnapshot() => ShowSettings(false);

    private void Custom_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AmountDialog { Owner = this };
        if (dialog.ShowDialog() == true) _viewModel.AddDrink(dialog.AmountOz);
    }

    private void ShowSettings(bool schedule)
    {
        LoadSettingsControls();
        SettingsHeading.Text = schedule ? "Your schedule" : "Your Waterline";
        SettingsOverlay.Visibility = Visibility.Visible;
        SettingsPanel.Opacity = 0;
        SettingsTranslate.X = 55;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        SettingsPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
        SettingsTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, new DoubleAnimation(55, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        if (schedule) StartBox.Focus(); else GoalBox.Focus();
    }

    private void CloseSettings()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease };
        fade.Completed += (_, _) => SettingsOverlay.Visibility = Visibility.Collapsed;
        SettingsPanel.BeginAnimation(OpacityProperty, fade);
        SettingsTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, new DoubleAnimation(0, 35, TimeSpan.FromMilliseconds(170)) { EasingFunction = ease });
    }

    private void CloseSettings_Click(object sender, RoutedEventArgs e) => CloseSettings();
    private void SettingsBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, SettingsOverlay)) CloseSettings();
    }

    private void Interval_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton selected) return;
        _selectedInterval = int.Parse(selected.Tag.ToString()!);
        foreach (var button in IntervalButtons()) button.IsChecked = ReferenceEquals(button, selected);
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(GoalBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var goal) || goal is < 8 or > 512 ||
            !TimeSpan.TryParse(StartBox.Text, out var start) || !TimeSpan.TryParse(EndBox.Text, out var end) || start >= end)
        {
            UpdateStatus.Text = "Check the daily goal and schedule times.";
            return;
        }
        var settings = _viewModel.Settings;
        settings.DailyGoalOz = goal;
        settings.ReminderIntervalMinutes = _selectedInterval;
        settings.WorkdayStart = start;
        settings.WorkdayEnd = end;
        settings.RemindersEnabled = RemindersBox.IsChecked == true;
        settings.SoundsEnabled = SoundsBox.IsChecked == true;
        settings.ReminderDays.Clear();
        foreach (var pair in DayButtons()) if (pair.Value.IsChecked == true) settings.ReminderDays.Add(pair.Key);
        _viewModel.SaveSettings();
        UpdateStatus.Text = "Changes saved on this PC.";
        CloseSettings();
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (_availableRelease is null) { await CheckForUpdatesAsync(true); return; }
        UpdateButton.IsEnabled = false;
        try
        {
            var progress = new Progress<double>(value => UpdateStatus.Text = $"Downloading update… {value:0}%");
            await _updates.DownloadAndInstallAsync(_availableRelease, progress);
            UpdateStatus.Text = "Installer ready. Follow the setup window to finish.";
        }
        catch { UpdateStatus.Text = "The update could not be downloaded. Try again shortly."; }
        finally { UpdateButton.IsEnabled = true; }
    }

    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (manual) UpdateStatus.Text = "Checking for updates…";
        UpdateButton.IsEnabled = false;
        try
        {
            var release = await _updates.CheckAsync();
            if (release is not null && release.Version > _updates.CurrentVersion)
            {
                _availableRelease = release;
                UpdateStatus.Text = $"Waterline {release.Version} is ready.";
                UpdateButton.Content = "Install update";
            }
            else UpdateStatus.Text = $"Waterline {_updates.CurrentVersion.ToString(3)} is up to date.";
        }
        catch { if (manual) UpdateStatus.Text = "Could not check right now. Try again shortly."; }
        finally { UpdateButton.IsEnabled = true; }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void WindowClose_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose) { e.Cancel = true; Hide(); }
        base.OnClosing(e);
    }

    public void AllowClose()
    {
        _allowClose = true;
        _viewModel.Dispose();
        Close();
    }
}
