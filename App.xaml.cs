using System.Diagnostics;
using System.Threading;
using System.Windows;

namespace Waterline;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showWindowEvent;
    private CancellationTokenSource? _instanceListenerCancellation;
    private TrayService? _tray;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var isSnapshot = Array.IndexOf(e.Args, "--snapshot") >= 0;
        var mutexName = isSnapshot ? $"Waterline.Native.Windows.Snapshot.{Environment.ProcessId}" : "Waterline.Native.Windows.SingleInstance";
        _singleInstanceMutex = new Mutex(true, mutexName, out var createdNew);
        if (!createdNew)
        {
            try { EventWaitHandle.OpenExisting("Waterline.Native.Windows.ShowMain").Set(); } catch { }
            Current.Shutdown();
            return;
        }

        _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, isSnapshot ? $"Waterline.Native.Windows.SnapshotShow.{Environment.ProcessId}" : "Waterline.Native.Windows.ShowMain");
        _instanceListenerCancellation = new CancellationTokenSource();

        var store = new AppStateStore();
        var viewModel = new MainViewModel(store);
        _mainWindow = new MainWindow(viewModel);
        var snapshotIndex = Array.IndexOf(e.Args, "--snapshot");
        if (snapshotIndex >= 0 && snapshotIndex + 1 < e.Args.Length)
        {
            _mainWindow.Show();
            _ = CaptureSnapshotAsync(viewModel, e.Args[snapshotIndex + 1], snapshotIndex + 2 < e.Args.Length ? e.Args[snapshotIndex + 2] : "main");
            return;
        }
        _tray = new TrayService(viewModel, ShowMainWindow, ExitApplication);
        viewModel.NotificationRequested += (_, notification) =>
            _tray.ShowNotification(notification.Title, notification.Message);
        _mainWindow.Show();
        _ = ListenForSecondInstanceAsync(_instanceListenerCancellation.Token);
    }

    private async Task CaptureSnapshotAsync(MainViewModel viewModel, string path, string mode)
    {
        Window target = _mainWindow!;
        if (mode == "settings") _mainWindow!.ShowSettingsForSnapshot();
        if (mode is "widget" or "collapsed")
        {
            _mainWindow!.Hide();
            var widget = new WidgetWindow(viewModel);
            widget.Show();
            if (mode == "collapsed") widget.SetCollapsedForSnapshot();
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
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Run(() => _showWindowEvent.WaitOne(), cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
                await Dispatcher.InvokeAsync(ShowMainWindow);
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null) return;
        if (_mainWindow.WindowState == WindowState.Minimized) _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Show();
        _mainWindow.Activate();
    }

    private void ExitApplication()
    {
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
