using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Waterline;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly GitHubUpdateService _updates = new();
    private ReleaseInfo? _availableRelease;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        IntervalBox.ItemsSource = new[] { 30, 45, 60, 90, 120 };
        LoadSettingsControls();
        Loaded += async (_, _) => await CheckForUpdatesAsync(false);
    }

    private void LoadSettingsControls()
    {
        var settings = _viewModel.Settings;
        IntervalBox.SelectedItem = settings.ReminderIntervalMinutes;
        StartBox.Text = settings.WorkdayStart.ToString(@"hh\:mm");
        EndBox.Text = settings.WorkdayEnd.ToString(@"hh\:mm");
        var boxes = DayBoxes();
        foreach (var pair in boxes) pair.Value.IsChecked = settings.ReminderDays.Contains(pair.Key);
    }

    private Dictionary<DayOfWeek, System.Windows.Controls.CheckBox> DayBoxes() => new()
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
    private void Settings_Click(object sender, RoutedEventArgs e) => SettingsCard.BringIntoView();

    private void Custom_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AmountDialog { Owner = this };
        if (dialog.ShowDialog() == true) _viewModel.AddDrink(dialog.AmountOz);
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(GoalBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var goal) || goal is < 8 or > 512 ||
            !TimeSpan.TryParse(StartBox.Text, out var start) || !TimeSpan.TryParse(EndBox.Text, out var end) || start >= end)
        {
            System.Windows.MessageBox.Show(this, "Use a goal from 8–512 oz and a valid start time earlier than the end time.", "Check settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var settings = _viewModel.Settings;
        settings.DailyGoalOz = goal;
        settings.ReminderIntervalMinutes = IntervalBox.SelectedItem as int? ?? 60;
        settings.WorkdayStart = start;
        settings.WorkdayEnd = end;
        settings.RemindersEnabled = RemindersBox.IsChecked == true;
        settings.SoundsEnabled = SoundsBox.IsChecked == true;
        settings.ReminderDays.Clear();
        foreach (var pair in DayBoxes()) if (pair.Value.IsChecked == true) settings.ReminderDays.Add(pair.Key);
        _viewModel.SaveSettings();
        UpdateStatus.Text = "Settings saved locally on this Windows PC.";
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (_availableRelease is null) { await CheckForUpdatesAsync(true); return; }
        UpdateButton.IsEnabled = false;
        try
        {
            var progress = new Progress<double>(value => UpdateStatus.Text = $"Downloading update… {value:0}%");
            await _updates.DownloadAndInstallAsync(_availableRelease, progress);
            UpdateStatus.Text = "Installer opened. Waterline will close when setup is complete.";
        }
        catch (Exception exception) { UpdateStatus.Text = $"Update failed: {exception.Message}"; }
        finally { UpdateButton.IsEnabled = true; }
    }

    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (manual) UpdateStatus.Text = "Checking GitHub Releases…";
        UpdateButton.IsEnabled = false;
        try
        {
            var release = await _updates.CheckAsync();
            if (release is not null && release.Version > _updates.CurrentVersion)
            {
                _availableRelease = release;
                UpdateStatus.Text = $"Waterline {release.Version} is available from GitHub.";
                UpdateButton.Content = "Download and install";
            }
            else UpdateStatus.Text = $"Waterline {_updates.CurrentVersion.ToString(3)} is up to date.";
        }
        catch (Exception exception)
        {
            if (manual) UpdateStatus.Text = $"Could not reach GitHub: {exception.Message}";
        }
        finally { UpdateButton.IsEnabled = true; }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    public void AllowClose()
    {
        _allowClose = true;
        _viewModel.Dispose();
        Close();
    }
}
