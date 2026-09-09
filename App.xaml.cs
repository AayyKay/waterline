using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using Waterline.Infrastructure;

namespace Waterline;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showWindowEvent;
    private CancellationTokenSource? _instanceListenerCancellation;
    private TrayService? _tray;
    private MainWindow? _mainWindow;
    private MainViewModel? _viewModel;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeManager.Initialize();
        var isSnapshot = Array.IndexOf(e.Args, "--snapshot") >= 0;
        var userScope = WindowsIdentity.GetCurrent().User?.Value?.Replace('-', '.') ?? Environment.UserName;
        var mutexName = isSnapshot ? $"Waterline.Native.Windows.Snapshot.{Environment.ProcessId}" : $"Local\\Waterline.Native.SingleInstance.{userScope}";
        var activationName = isSnapshot ? $"Waterline.Native.Windows.SnapshotShow.{Environment.ProcessId}" : $"Local\\Waterline.Native.ShowMain.{userScope}";
        _singleInstanceMutex = new Mutex(true, mutexName, out var createdNew);
        if (!createdNew)
        {
            try { EventWaitHandle.OpenExisting(activationName).Set(); } catch { }
            Current.Shutdown();
            return;
        }

        _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, activationName);
        _instanceListenerCancellation = new CancellationTokenSource();

        var statePath = isSnapshot
            ? Path.Combine(Path.GetTempPath(), "Waterline", "Snapshots", Environment.ProcessId.ToString(), "state.json")
            : null;
        var store = new AppStateStore(statePath);
        var viewModel = new MainViewModel(store);
        _viewModel = viewModel;
        _mainWindow = new MainWindow(viewModel, enableUpdateChecks: !isSnapshot);
        _mainWindow.InstallRequested += (_, _) => ExitApplication();
        _mainWindow.OpenWidgetRequested += (_, _) => ShowWidget();
        var snapshotIndex = Array.IndexOf(e.Args, "--snapshot");
        if (snapshotIndex >= 0 && snapshotIndex + 1 < e.Args.Length)
        {
            var mode = snapshotIndex + 2 < e.Args.Length ? e.Args[snapshotIndex + 2] : "main";
            _mainWindow.PrepareForSnapshot(mode);
            _mainWindow.Show();
            _ = CaptureSnapshotAsync(viewModel, e.Args[snapshotIndex + 1], mode);
            return;
        }
        _tray = new TrayService(viewModel, ShowMainWindow, ShowWidget, ExitApplication);
        viewModel.NotificationRequested += (_, notification) =>
            _tray.ShowNotification(notification.Title, notification.Message);
        _mainWindow.Show();
        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
        SystemEvents.TimeChanged += SystemEvents_TimeChanged;
        _ = ListenForSecondInstanceAsync(_instanceListenerCancellation.Token);
    }

    private async Task CaptureSnapshotAsync(MainViewModel viewModel, string path, string mode)
    {
        Window target = _mainWindow!;
        if (mode == "settings") _mainWindow!.ShowSettingsForSnapshot();
        if (mode is "dialog" or "dialog-invalid")
        {
            _mainWindow!.Hide();
            var dialog = new AmountDialog { Owner = _mainWindow };
            dialog.Show();
            if (mode == "dialog-invalid") dialog.PrepareInvalidForSnapshot();
            target = dialog;
        }
        if (mode == "unsaved")
        {
            _mainWindow!.Hide();
            var dialog = new UnsavedChangesDialog("Schedule") { Owner = _mainWindow };
            dialog.Show();
            target = dialog;
        }
        if (mode is "widget" or "collapsed" or "widget-high-contrast" or "collapsed-high-contrast")
        {
            _mainWindow!.Hide();
            var widget = new WidgetWindow(viewModel, ShowMainWindow);
            widget.Show();
            if (mode is "collapsed" or "collapsed-high-contrast") widget.SetCollapsedForSnapshot();
            target = widget;
        }
        await Task.Delay(700);
        VisualSnapshot.Save(target, path);
        target.Close();
        viewModel.Dispose();
        Shutdown();
    }

    private async Task ListenForSecondInstanceAsync(CancellationToken cancellationToken)
    {
        if (_showWindowEvent is null) return;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Run(() => _showWindowEvent.WaitOne(), cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                    await Dispatcher.InvokeAsync(ShowMainWindow);
            }
        }
        catch (OperationCanceledException) { }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null) return;
        WindowActivation.Restore(_mainWindow);
    }

    private void ShowWidget()
    {
        if (_mainWindow?.DataContext is MainViewModel viewModel)
            WidgetWindow.ShowOrActivate(viewModel, ShowMainWindow);
    }

    private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
            Dispatcher.BeginInvoke(() => _viewModel?.RefreshAfterSystemResume());
    }

    private void SystemEvents_TimeChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(() => _viewModel?.RefreshFromSystemClock());

    private void ExitApplication()
    {
        if (_exiting) return;
        _exiting = true;
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
        SystemEvents.TimeChanged -= SystemEvents_TimeChanged;
        WidgetWindow.CloseCurrent();
        _mainWindow?.AllowClose();
        _tray?.Dispose();
        _instanceListenerCancellation?.Cancel();
        _showWindowEvent?.Set();
        _showWindowEvent?.Dispose();
        _instanceListenerCancellation?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        Current.Shutdown();
    }
}
