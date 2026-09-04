using System.Drawing;
using Forms = System.Windows.Forms;

namespace Waterline;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;

    public TrayService(MainViewModel viewModel, Action showMain, Action exit)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Waterline", null, (_, _) => showMain());
        menu.Items.Add("Open mini widget", null, (_, _) => WidgetWindow.ShowOrActivate(viewModel));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => exit());
        _icon = new Forms.NotifyIcon
        {
            Text = "Waterline",
            Icon = SystemIcons.Information,
            Visible = true,
            ContextMenuStrip = menu
        };
        _icon.DoubleClick += (_, _) => showMain();
    }

    public void ShowNotification(string title, string message)
    {
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = message;
        _icon.BalloonTipIcon = Forms.ToolTipIcon.Info;
        _icon.ShowBalloonTip(8000);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
