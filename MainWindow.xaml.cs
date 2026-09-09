using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Waterline;

public partial class MainWindow : Window
{
    private const double CompactNavigationWidth = 1040;
    private readonly MainViewModel _viewModel;
    private readonly bool _enableUpdateChecks;
    private readonly ToggleButton[] _destinationButtons;
    private bool _allowClose;
    private bool _ambientMotionRunning;
    private bool _focusLogOnLoad;
    private double _nextReservoirResponse = 1;
    private string _selectedDestination = "Today";

    public MainWindow(MainViewModel viewModel, bool enableUpdateChecks = true)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _enableUpdateChecks = enableUpdateChecks;
        DataContext = viewModel;
        _destinationButtons = [TodayNav, InsightsNav, GoalsNav, ScheduleNav, WidgetNav, SettingsNav];
        SettingsView.InstallRequested += (_, _) => InstallRequested?.Invoke(this, EventArgs.Empty);
        WidgetView.OpenRequested += (_, _) => OpenWidgetRequested?.Invoke(this, EventArgs.Empty);
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
        IsVisibleChanged += (_, _) => UpdateMotionState();
        Loaded += OnLoaded;
    }

    public event EventHandler? InstallRequested;
    public event EventHandler? OpenWidgetRequested;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout(ActualWidth);
        if (MotionPolicy.IsEnabled)
            ((Storyboard)Resources["ShellEntrance"]).Begin(this, true);
        else
        {
            ShellRoot.Opacity = 1;
            ShellTranslate.Y = 0;
        }
        UpdateMotionState();
        if (_focusLogOnLoad) Log12Button.Focus();
        await SettingsView.InitializeUpdatesAsync(_enableUpdateChecks);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.ReservoirFillHeight) || !IsLoaded) return;
        var response = _nextReservoirResponse;
        _nextReservoirResponse = 1;
        UpdateReservoirFill(MotionPolicy.IsEnabled && IsVisible && WindowState != WindowState.Minimized, response);
    }

    private void UpdateReservoirFill(bool animate, double response = 1)
    {
        var reservoirFrame = (Border)((Grid)ReservoirFill.Parent).Parent;
        var target = Math.Max(0, reservoirFrame.Height - 4) * _viewModel.ProgressPercent / 100;
        var previous = ReservoirFill.ActualHeight;
        ReservoirFill.BeginAnimation(HeightProperty, null);
        ReservoirFill.Height = target;
        if (!animate) return;
        var animation = new DoubleAnimation
        {
            From = previous,
            To = target,
            Duration = TimeSpan.FromMilliseconds(500),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        ReservoirFill.BeginAnimation(HeightProperty, animation, HandoffBehavior.SnapshotAndReplace);
        ReservoirSurfaceScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimationUsingKeyFrames
        {
            KeyFrames =
            {
                new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)),
                new EasingDoubleKeyFrame(1 + .34 * response, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))),
                new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1050)))
            }
        }, HandoffBehavior.SnapshotAndReplace);
        ReservoirHighlightTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
        {
            From = 70,
            To = -Math.Max(70, target),
            Duration = TimeSpan.FromMilliseconds(900),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        }, HandoffBehavior.SnapshotAndReplace);
        ReservoirHighlight.BeginAnimation(OpacityProperty, new DoubleAnimationUsingKeyFrames
        {
            KeyFrames =
            {
                new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)),
                new EasingDoubleKeyFrame(.28 * response, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))),
                new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(900)))
            }
        }, HandoffBehavior.SnapshotAndReplace);
        TotalValueText.BeginAnimation(OpacityProperty, new DoubleAnimation(.58, 1, TimeSpan.FromMilliseconds(190)), HandoffBehavior.SnapshotAndReplace);
    }

    private void SystemParameters_StaticPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SystemParameters.ClientAreaAnimation) or nameof(SystemParameters.HighContrast))
            UpdateMotionState();
    }

    private void UpdateMotionState()
    {
        if (!IsLoaded) return;
        var shouldRun = MotionPolicy.IsEnabled && IsVisible && WindowState != WindowState.Minimized;
        var ambient = (Storyboard)Resources["AmbientMotion"];
        if (shouldRun && !_ambientMotionRunning)
        {
            ambient.Begin(this, true);
            _ambientMotionRunning = true;
        }
        else if (!shouldRun && _ambientMotionRunning)
        {
            ambient.Remove(this);
            AmbientCurrentTranslate.X = 0;
            ReservoirWaveTranslate.X = 0;
            _ambientMotionRunning = false;
        }
    }

    private void Navigation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button || button.Tag is not string destination) return;
        if (!SelectDestination(destination)) return;
        if (destination == "Widget") OpenWidgetRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool SelectDestination(string destination, bool skipPrompt = false)
    {
        if (destination == _selectedDestination)
        {
            ApplyDestinationSelection(destination);
            return true;
        }
        if (!skipPrompt && !ConfirmNavigation())
        {
            ApplyDestinationSelection(_selectedDestination);
            return false;
        }
        _selectedDestination = destination;
        ApplyDestinationSelection(destination);
        MainScroll.ScrollToTop();
        return true;
    }

    private void ApplyDestinationSelection(string destination)
    {
        foreach (var button in _destinationButtons)
        {
            var selected = Equals(button.Tag, destination);
            button.IsChecked = selected;
            System.Windows.Automation.AutomationProperties.SetName(button, selected ? $"{destination}, selected" : destination);
        }
        MoreNav.IsChecked = destination is "Widget" or "Settings" && MoreNav.Visibility == Visibility.Visible;
        TodayView.Visibility = destination == "Today" ? Visibility.Visible : Visibility.Collapsed;
        InsightsView.Visibility = destination == "Insights" ? Visibility.Visible : Visibility.Collapsed;
        GoalsView.Visibility = destination == "Goals" ? Visibility.Visible : Visibility.Collapsed;
        ScheduleView.Visibility = destination == "Schedule" ? Visibility.Visible : Visibility.Collapsed;
        SettingsView.Visibility = destination == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        WidgetView.Visibility = destination == "Widget" ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool ConfirmNavigation()
    {
        var configuration = _viewModel.Configuration;
        var hasChanges = _selectedDestination == "Schedule"
            ? configuration.HasScheduleChanges
            : _selectedDestination is "Goals" or "Settings" && configuration.HasSettingsChanges;
        if (!hasChanges) return true;

        var dialog = new UnsavedChangesDialog(_selectedDestination) { Owner = this };
        if (dialog.ShowDialog() != true) return false;
        if (dialog.Choice == UnsavedChoice.Discard)
        {
            if (_selectedDestination == "Schedule") configuration.CancelSchedule(); else configuration.CancelSettings();
            return true;
        }
        if (dialog.Choice == UnsavedChoice.Save)
            return _selectedDestination == "Schedule" ? configuration.SaveSchedule() : configuration.SaveSettings();
        return false;
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        MoreMenu.PlacementTarget = MoreNav;
        MoreMenu.Placement = PlacementMode.Bottom;
        MoreMenu.IsOpen = true;
    }

    private void WidgetMenu_Click(object sender, RoutedEventArgs e)
    {
        if (!SelectDestination("Widget")) return;
        OpenWidgetRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SettingsMenu_Click(object sender, RoutedEventArgs e) => SelectDestination("Settings");

    private void Add8_Click(object sender, RoutedEventArgs e) => LogWater(8);
    private void Add12_Click(object sender, RoutedEventArgs e) => LogWater(12);
    private void Add16_Click(object sender, RoutedEventArgs e) => LogWater(16);
    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        _nextReservoirResponse = .55;
        _viewModel.UndoLastDrink();
    }
    private void Recover_Click(object sender, RoutedEventArgs e) => _viewModel.AcknowledgeRecovery();

    private void Custom_Click(object sender, RoutedEventArgs e)
    {
        var opener = sender as System.Windows.Controls.Button;
        var dialog = new AmountDialog { Owner = this };
        if (dialog.ShowDialog() == true) LogWater(dialog.AmountOz);
        opener?.Focus();
    }

    private void LogWater(double amount)
    {
        _nextReservoirResponse = 1;
        _viewModel.AddDrink(amount);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyResponsiveLayout(e.NewSize.Width);

    private void ApplyResponsiveLayout(double width)
    {
        if (!IsInitialized) return;
        var compact = width < CompactNavigationWidth;
        WidgetNav.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        SettingsNav.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        MoreNav.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        MoreNav.IsChecked = compact && _selectedDestination is "Widget" or "Settings";

        if (compact)
        {
            Grid.SetColumnSpan(TodayPrimary, 3);
            Grid.SetRow(TodayRail, 1);
            Grid.SetColumn(TodayRail, 0);
            Grid.SetColumnSpan(TodayRail, 3);
            var progressCard = (Border)TodayPrimary.Children[0];
            progressCard.Padding = new Thickness(12);
            var reservoirFrame = (Border)((Grid)ReservoirFill.Parent).Parent;
            reservoirFrame.Height = 145;
            var reservoirArea = (Grid)reservoirFrame.Parent;
            reservoirArea.Height = 160;
            ((StackPanel)reservoirArea.Children[1]).Visibility = Visibility.Collapsed;
            var historyCard = (Border)((StackPanel)HistoryDayList.Parent).Parent;
            Grid.SetColumnSpan(historyCard, 3);
            Grid.SetRow(HistoryDetail, 1);
            Grid.SetColumn(HistoryDetail, 0);
            Grid.SetColumnSpan(HistoryDetail, 3);
        }
        else
        {
            Grid.SetColumnSpan(TodayPrimary, 1);
            Grid.SetRow(TodayRail, 0);
            Grid.SetColumn(TodayRail, 2);
            Grid.SetColumnSpan(TodayRail, 1);
            var progressCard = (Border)TodayPrimary.Children[0];
            progressCard.Padding = new Thickness(24);
            var reservoirFrame = (Border)((Grid)ReservoirFill.Parent).Parent;
            reservoirFrame.Height = 270;
            var reservoirArea = (Grid)reservoirFrame.Parent;
            reservoirArea.Height = 292;
            ((StackPanel)reservoirArea.Children[1]).Visibility = Visibility.Visible;
            var historyCard = (Border)((StackPanel)HistoryDayList.Parent).Parent;
            Grid.SetColumnSpan(historyCard, 1);
            Grid.SetRow(HistoryDetail, 0);
            Grid.SetColumn(HistoryDetail, 2);
            Grid.SetColumnSpan(HistoryDetail, 1);
        }
        UpdateReservoirFill(false);
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        var destination = e.Key switch
        {
            Key.D1 or Key.NumPad1 => "Today",
            Key.D2 or Key.NumPad2 => "Insights",
            Key.D3 or Key.NumPad3 => "Goals",
            Key.D4 or Key.NumPad4 => "Schedule",
            Key.D5 or Key.NumPad5 => "Widget",
            Key.D6 or Key.NumPad6 => "Settings",
            _ => null
        };
        if (destination is null) return;
        if (!SelectDestination(destination)) return;
        if (destination == "Widget") OpenWidgetRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void Window_Activated(object? sender, EventArgs e) => _viewModel.RefreshFromSystemClock();

    public void ShowSettingsForSnapshot() => SelectDestination("Settings", true);

    public void PrepareForSnapshot(string mode)
    {
        _viewModel.PrepareSnapshotFixture(mode);
        _viewModel.Configuration.PrepareSnapshot(mode);
        switch (mode)
        {
            case "compact":
                Width = MinWidth;
                Height = MinHeight;
                break;
            case "schedule-compact":
                Width = MinWidth;
                Height = MinHeight;
                SelectDestination("Schedule", true);
                break;
            case "settings-compact":
                Width = MinWidth;
                Height = MinHeight;
                SelectDestination("Settings", true);
                break;
            case "focus":
                _focusLogOnLoad = true;
                break;
            case "high-contrast":
                ThemeManager.ApplyHighContrastForSnapshot();
                break;
            case "reduced-motion":
            case "widget-reduced-motion":
            case "collapsed-reduced-motion":
                MotionPolicy.ForceReducedForSnapshot();
                break;
            case "motion-today":
            case "motion-widget":
            case "motion-collapsed":
            case "motion-log":
                break;
            case "schedule-high-contrast":
                ThemeManager.ApplyHighContrastForSnapshot();
                SelectDestination("Schedule", true);
                break;
            case "settings-high-contrast":
                ThemeManager.ApplyHighContrastForSnapshot();
                SelectDestination("Settings", true);
                break;
            case "history":
                SelectDestination("Insights", true);
                break;
            case "goals":
                SelectDestination("Goals", true);
                break;
            case "widget-page":
                SelectDestination("Widget", true);
                break;
            case "widget-page-compact":
                Width = MinWidth;
                Height = MinHeight;
                SelectDestination("Widget", true);
                break;
            case "widget-high-contrast":
            case "collapsed-high-contrast":
                ThemeManager.ApplyHighContrastForSnapshot();
                break;
            case "schedule":
            case "schedule-dirty":
            case "schedule-invalid":
            case "unsaved":
                SelectDestination("Schedule", true);
                break;
            case "settings":
            case "settings-dirty":
            case "update-checking":
            case "update-available":
            case "update-downloading":
            case "update-ready":
            case "update-failed":
                SelectDestination("Settings", true);
                break;
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (!IsInitialized) return;
        MaximizeGlyph.Data = (Geometry)FindResource(WindowState == WindowState.Maximized ? "IconRestore" : "IconMaximize");
        UpdateMotionState();
    }

    private void WindowClose_Click(object sender, RoutedEventArgs e) => Close();

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
        ((Storyboard)Resources["AmbientMotion"]).Remove(this);
        SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.Dispose();
        Close();
    }
}
