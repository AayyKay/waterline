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
    private bool _focusSampleOnLoad;
    private bool _scrollEndOnLoad;
    private string _selectedDestination = "Today";

    public MainWindow(MainViewModel viewModel, bool enableUpdateChecks = true)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _destinationButtons = [TodayNav, InsightsNav, GoalsNav, ScheduleNav, WidgetNav, SettingsNav];
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
        if (_focusSampleOnLoad) SecondarySampleButton.Focus();
        if (_scrollEndOnLoad) MainScroll.ScrollToEnd();
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
        PageEyebrow.Text = $"{destination.ToUpperInvariant()} · NATIVE SYSTEM";
        PageTitle.Text = destination == "Today" ? "Native component foundation" : $"{destination} foundation";
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

    private void SampleMenu_Click(object sender, RoutedEventArgs e)
    {
        SampleMenu.PlacementTarget = MenuSampleButton;
        SampleMenu.Placement = PlacementMode.Bottom;
        SampleMenu.IsOpen = true;
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
            Grid.SetRow(ActivityRail, 1);
            Grid.SetColumn(ActivityRail, 0);
            Grid.SetColumnSpan(ActivityRail, 3);
            ActivityRail.Margin = new Thickness(0);
        }
        else
        {
            Grid.SetRow(ActivityRail, 0);
            Grid.SetColumn(ActivityRail, 2);
            Grid.SetColumnSpan(ActivityRail, 1);
            ActivityRail.Margin = new Thickness(0);
        }
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

    public void ShowSettingsForSnapshot() => SelectDestination("Settings");

    public void PrepareForSnapshot(string mode)
    {
        switch (mode)
        {
            case "compact":
                Width = MinWidth;
                Height = MinHeight;
                break;
            case "focus":
                _focusSampleOnLoad = true;
                break;
            case "high-contrast":
                ThemeManager.ApplyHighContrastForSnapshot();
                break;
            case "bottom":
                _scrollEndOnLoad = true;
                break;
            case "high-contrast-bottom":
                ThemeManager.ApplyHighContrastForSnapshot();
                _scrollEndOnLoad = true;
                break;
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

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
        _viewModel.Dispose();
        Close();
    }
}
