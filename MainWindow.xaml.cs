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
    private readonly ToggleButton[] _destinationButtons;
    private bool _allowClose;
    private bool _focusLogOnLoad;
    private string _selectedDestination = "Today";

    public MainWindow(MainViewModel viewModel, bool enableUpdateChecks = true)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _destinationButtons = [TodayNav, InsightsNav, GoalsNav, ScheduleNav, WidgetNav, SettingsNav];
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout(ActualWidth);
        if (SystemParameters.ClientAreaAnimation)
            ((Storyboard)Resources["ShellEntrance"]).Begin(this, true);
        else
        {
            ShellRoot.Opacity = 1;
            ShellTranslate.Y = 0;
        }
        if (_focusLogOnLoad) Log12Button.Focus();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.ReservoirFillHeight) || !IsLoaded) return;
        UpdateReservoirFill(SystemParameters.ClientAreaAnimation);
    }

    private void UpdateReservoirFill(bool animate)
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
            Duration = TimeSpan.FromMilliseconds(240),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        ReservoirFill.BeginAnimation(HeightProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void Navigation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button || button.Tag is not string destination) return;
        SelectDestination(destination);
        if (destination == "Widget") WidgetWindow.ShowOrActivate(_viewModel);
    }

    private void SelectDestination(string destination)
    {
        _selectedDestination = destination;
        foreach (var button in _destinationButtons)
        {
            var selected = Equals(button.Tag, destination);
            button.IsChecked = selected;
            System.Windows.Automation.AutomationProperties.SetName(button, selected ? $"{destination}, selected" : destination);
        }
        MoreNav.IsChecked = destination is "Widget" or "Settings" && MoreNav.Visibility == Visibility.Visible;
        TodayView.Visibility = destination == "Today" ? Visibility.Visible : Visibility.Collapsed;
        InsightsView.Visibility = destination == "Insights" ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderView.Visibility = destination is "Today" or "Insights" ? Visibility.Collapsed : Visibility.Visible;
        if (PlaceholderView.Visibility == Visibility.Visible)
        {
            PlaceholderEyebrow.Text = destination is "Goals" ? "PHASE 5" : destination is "Schedule" or "Settings" ? "PHASE 5" : "PHASE 6";
            PlaceholderTitle.Text = destination == "Widget" ? "Widget opens separately" : $"{destination} is scheduled next";
        }
        MainScroll.ScrollToTop();
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        MoreMenu.PlacementTarget = MoreNav;
        MoreMenu.Placement = PlacementMode.Bottom;
        MoreMenu.IsOpen = true;
    }

    private void WidgetMenu_Click(object sender, RoutedEventArgs e)
    {
        SelectDestination("Widget");
        WidgetWindow.ShowOrActivate(_viewModel);
    }

    private void SettingsMenu_Click(object sender, RoutedEventArgs e) => SelectDestination("Settings");

    private void Add8_Click(object sender, RoutedEventArgs e) => _viewModel.AddDrink(8);
    private void Add12_Click(object sender, RoutedEventArgs e) => _viewModel.AddDrink(12);
    private void Add16_Click(object sender, RoutedEventArgs e) => _viewModel.AddDrink(16);
    private void Undo_Click(object sender, RoutedEventArgs e) => _viewModel.UndoLastDrink();
    private void Recover_Click(object sender, RoutedEventArgs e) => _viewModel.AcknowledgeRecovery();

    private void Custom_Click(object sender, RoutedEventArgs e)
    {
        var opener = sender as System.Windows.Controls.Button;
        var dialog = new AmountDialog { Owner = this };
        if (dialog.ShowDialog() == true) _viewModel.AddDrink(dialog.AmountOz);
        opener?.Focus();
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
        SelectDestination(destination);
        if (destination == "Widget") WidgetWindow.ShowOrActivate(_viewModel);
        e.Handled = true;
    }

    private void Window_Activated(object? sender, EventArgs e) => _viewModel.RefreshFromSystemClock();

    public void ShowSettingsForSnapshot() => SelectDestination("Settings");

    public void PrepareForSnapshot(string mode)
    {
        _viewModel.PrepareSnapshotFixture(mode);
        switch (mode)
        {
            case "compact":
                Width = MinWidth;
                Height = MinHeight;
                break;
            case "focus":
                _focusLogOnLoad = true;
                break;
            case "high-contrast":
                ThemeManager.ApplyHighContrastForSnapshot();
                break;
            case "history":
                SelectDestination("Insights");
                break;
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (!IsInitialized) return;
        MaximizeGlyph.Data = (Geometry)FindResource(WindowState == WindowState.Maximized ? "IconRestore" : "IconMaximize");
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
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.Dispose();
        Close();
    }
}
