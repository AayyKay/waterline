using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using Waterline.Core;
using Waterline.Infrastructure;
using Forms = System.Windows.Forms;

namespace Waterline;

public partial class WidgetWindow : Window
{
    private const double ExpandedWidth = 400;
    private const double ExpandedHeight = 500;
    private const double CompactWidth = 104;
    private const double CompactHeight = 168;
    private static WidgetWindow? _current;
    private readonly MainViewModel _viewModel;
    private readonly Action _showDashboard;
    private readonly DispatcherTimer _placementTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private string _mode;
    private bool _restoring;

    public WidgetWindow(MainViewModel viewModel, Action? showDashboard = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _showDashboard = showDashboard ?? ShowMainWindowFallback;
        _mode = viewModel.CurrentWidgetMode is "compact" ? "compact" : "expanded";
        DataContext = viewModel;
        _current = this;
        _placementTimer.Tick += PlacementTimer_Tick;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        Loaded += WidgetWindow_Loaded;
        LocationChanged += (_, _) => QueuePlacementSave();
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(ClampCurrentPlacement);
        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        Closed += WidgetWindow_Closed;
    }

    public static bool IsOpen => _current is { IsVisible: true };
    public static event EventHandler? AvailabilityChanged;

    public static void ShowOrActivate(MainViewModel viewModel, Action? showDashboard = null)
    {
        if (_current is { IsVisible: true } current)
        {
            current.Activate();
            return;
        }
        new WidgetWindow(viewModel, showDashboard).Show();
    }

    public static void CloseCurrent()
    {
        _current?.SavePlacement();
        _current?.Close();
    }

    private void WidgetWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyMode(_mode, restorePlacement: false, persist: false);
        RestorePlacement();
        SavePlacement();
        if (SystemParameters.ClientAreaAnimation)
            ((System.Windows.Media.Animation.Storyboard)Resources["WidgetEntrance"]).Begin(this, true);
        else
        {
            WidgetSurface.Opacity = 1;
            WidgetTranslate.Y = 0;
        }
        AvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void WidgetWindow_Closed(object? sender, EventArgs e)
    {
        SavePlacement();
        _placementTimer.Stop();
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        if (ReferenceEquals(_current, this)) _current = null;
        AvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(RestorePlacement);

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentWidgetMode) && _viewModel.CurrentWidgetMode != _mode)
            ApplyMode(_viewModel.CurrentWidgetMode, restorePlacement: true);
    }

    private void ClampCurrentPlacement()
    {
        var current = CapturePlacement();
        RestorePlacement(current);
        SavePlacement();
    }

    private void Add8_Click(object sender, RoutedEventArgs e) => _viewModel.AddDrink(8);
    private void Add12_Click(object sender, RoutedEventArgs e) => _viewModel.AddDrink(12);
    private void Add16_Click(object sender, RoutedEventArgs e) => _viewModel.AddDrink(16);
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void OpenDashboard_Click(object sender, RoutedEventArgs e) => _showDashboard();

    private void Custom_Click(object sender, RoutedEventArgs e)
    {
        var opener = sender as System.Windows.Controls.Button;
        var dialog = new AmountDialog { Owner = this };
        if (dialog.ShowDialog() == true) _viewModel.AddDrink(dialog.AmountOz);
        opener?.Focus();
    }

    private void Collapse_Click(object sender, RoutedEventArgs e) => ApplyMode("compact", restorePlacement: true);
    private void Expand_Click(object sender, RoutedEventArgs e) => ApplyMode("expanded", restorePlacement: true);
    public void SetCollapsedForSnapshot() => ApplyMode("compact", restorePlacement: true);

    private void ApplyMode(string mode, bool restorePlacement, bool persist = true)
    {
        var placement = CapturePlacement();
        _mode = mode is "compact" ? "compact" : "expanded";
        var compact = _mode == "compact";
        ExpandedCard.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        CompactCard.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        Width = compact ? CompactWidth : ExpandedWidth;
        Height = compact ? CompactHeight : ExpandedHeight;
        MinWidth = MaxWidth = Width;
        MinHeight = MaxHeight = Height;
        if (restorePlacement) RestorePlacement(placement);
        if (persist) SavePlacement();
    }

    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed) return;
        if (FindButtonAncestor(e.OriginalSource as DependencyObject) is not null) return;
        try
        {
            DragMove();
            SavePlacement();
        }
        catch (InvalidOperationException) { }
    }

    private static System.Windows.Controls.Primitives.ButtonBase? FindButtonAncestor(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase button) return button;
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }
        return null;
    }

    private void QueuePlacementSave()
    {
        if (_restoring || !IsLoaded) return;
        _placementTimer.Stop();
        _placementTimer.Start();
    }

    private void PlacementTimer_Tick(object? sender, EventArgs e)
    {
        _placementTimer.Stop();
        SavePlacement();
    }

    private void SavePlacement()
    {
        if (!IsLoaded || _restoring) return;
        var placement = CapturePlacement();
        if (placement is not null) _viewModel.TrySaveWidgetLayout(_mode, placement);
    }

    private WidgetPlacement? CapturePlacement()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var rect)) return _viewModel.CopyWidgetPlacement();
        var screen = Forms.Screen.FromHandle(handle);
        var area = new WidgetWorkArea(screen.DeviceName, screen.WorkingArea.Left, screen.WorkingArea.Top, screen.WorkingArea.Width, screen.WorkingArea.Height);
        var captured = WidgetPlacementPolicy.Capture(
            new WidgetBounds(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top),
            area,
            GetDpiForWindowSafe(handle) / 96d);
        captured.Width = Width;
        captured.Height = Height;
        return captured;
    }

    private void RestorePlacement() => RestorePlacement(_viewModel.CopyWidgetPlacement());

    private void RestorePlacement(WidgetPlacement? placement)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        var screens = Forms.Screen.AllScreens;
        var areas = screens.Select(screen => new WidgetWorkArea(
            screen.DeviceName, screen.WorkingArea.Left, screen.WorkingArea.Top,
            screen.WorkingArea.Width, screen.WorkingArea.Height)).ToArray();
        var fallback = Forms.Screen.PrimaryScreen?.DeviceName ?? areas[0].MonitorId;
        var target = screens.FirstOrDefault(screen => string.Equals(screen.DeviceName, placement?.MonitorId, StringComparison.OrdinalIgnoreCase))
                     ?? Forms.Screen.PrimaryScreen ?? screens[0];
        var dpiScale = GetDpiForScreen(target) / 96d;
        var bounds = WidgetPlacementPolicy.Restore(placement, areas, fallback, Width * dpiScale, Height * dpiScale);
        _restoring = true;
        try
        {
            SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(bounds.Left), (int)Math.Round(bounds.Top),
                (int)Math.Round(bounds.Width), (int)Math.Round(bounds.Height), 0x0014);
        }
        finally { _restoring = false; }
    }

    private static uint GetDpiForWindowSafe(IntPtr handle)
    {
        try { return GetDpiForWindow(handle); }
        catch (EntryPointNotFoundException) { return 96; }
    }

    private static uint GetDpiForScreen(Forms.Screen screen)
    {
        try
        {
            var point = new NativePoint(screen.Bounds.Left + screen.Bounds.Width / 2, screen.Bounds.Top + screen.Bounds.Height / 2);
            var monitor = MonitorFromPoint(point, 2);
            return GetDpiForMonitor(monitor, 0, out var x, out _) == 0 ? x : 96;
        }
        catch (DllNotFoundException) { return 96; }
        catch (EntryPointNotFoundException) { return 96; }
    }

    private void ShowMainWindowFallback()
    {
        if (System.Windows.Application.Current.MainWindow is MainWindow main)
        {
            if (main.WindowState == WindowState.Minimized) main.WindowState = WindowState.Normal;
            main.Show();
            main.Activate();
        }
    }

    [StructLayout(LayoutKind.Sequential)] private readonly record struct NativePoint(int X, int Y);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
}
