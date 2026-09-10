using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Waterline;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    public SettingsView() => InitializeComponent();
    public event EventHandler? InstallRequested;

    public Task InitializeUpdatesAsync(bool enabled) =>
        (DataContext as MainViewModel)?.Configuration.InitializeUpdatesAsync(enabled) ?? Task.CompletedTask;

    private void Save_Click(object sender, RoutedEventArgs e) => (DataContext as MainViewModel)?.Configuration.SaveSettings();
    private void Cancel_Click(object sender, RoutedEventArgs e) => (DataContext as MainViewModel)?.Configuration.CancelSettings();
    private async void CheckUpdate_Click(object sender, RoutedEventArgs e) { if (DataContext is MainViewModel vm) await vm.Configuration.CheckForUpdatesAsync(); }
    private async void DownloadUpdate_Click(object sender, RoutedEventArgs e) { if (DataContext is MainViewModel vm) await vm.Configuration.DownloadUpdateAsync(); }
    private void OpenRelease_Click(object sender, RoutedEventArgs e) => (DataContext as MainViewModel)?.Configuration.OpenReleasePage();

    private void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if ((DataContext as MainViewModel)?.Configuration.LaunchInstaller() == true)
            InstallRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Recover_Click(object sender, RoutedEventArgs e) => (DataContext as MainViewModel)?.AcknowledgeRecovery();
    private void ImportLegacy_Click(object sender, RoutedEventArgs e) => (DataContext as MainViewModel)?.Configuration.ImportLegacyData();
    private void ClearDiagnostics_Click(object sender, RoutedEventArgs e) => (DataContext as MainViewModel)?.Configuration.ClearDiagnostics();

    private void OpenData_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{vm.StateFilePath}\"") { UseShellExecute = true });
    }
}
