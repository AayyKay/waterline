using System.Drawing;
using Forms = System.Windows.Forms;

namespace Waterline;

public sealed class TrayService : IDisposable
{
    private readonly MainViewModel _viewModel;
    private readonly Action _showMain;
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.ToolStripMenuItem _pauseItem;

    public TrayService(MainViewModel viewModel, Action showMain, Action showWidget, Action exit)
    {
        _viewModel = viewModel;
        _showMain = showMain;
        _menu = new Forms.ContextMenuStrip
        {
            BackColor = Color.FromArgb(7, 21, 38),
            ForeColor = Color.FromArgb(242, 248, 252),
            ShowImageMargin = false,
            Padding = new Forms.Padding(7),
            Renderer = new WaterlineMenuRenderer()
        };
        _menu.Opening += (_, _) => UpdateMenuState();

        _menu.Items.Add(MenuItem("Open Waterline", (_, _) => showMain(), "Open the Waterline dashboard"));
        _menu.Items.Add(MenuItem("Open Widget", (_, _) => showWidget(), "Open or focus the Waterline widget"));
        _pauseItem = MenuItem("Pause reminders", (_, _) => ToggleReminders(), "Pause or resume scheduled reminders");
        _menu.Items.Add(_pauseItem);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(MenuItem("Quit", (_, _) => exit(), "Quit Waterline completely"));

        var iconResource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/waterline-tray.ico"));
        using var loadedIcon = iconResource is null ? SystemIcons.Application : new Icon(iconResource.Stream);
        _icon = new Forms.NotifyIcon
        {
            Text = "Waterline · local hydration",
            Icon = (Icon)loadedIcon.Clone(),
            Visible = true,
            ContextMenuStrip = _menu
        };
        _icon.DoubleClick += (_, _) => showMain();
        _icon.BalloonTipClicked += (_, _) => showMain();
        UpdateMenuState();
    }

    public void ShowNotification(string title, string message)
    {
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = message;
        _icon.BalloonTipIcon = Forms.ToolTipIcon.None;
        _icon.ShowBalloonTip(8000);
    }

    private static Forms.ToolStripMenuItem MenuItem(string text, EventHandler click, string accessibleName)
    {
        var item = new Forms.ToolStripMenuItem(text) { AccessibleName = accessibleName, Padding = new Forms.Padding(8, 5, 20, 5) };
        item.Click += click;
        return item;
    }

    private void ToggleReminders()
    {
        if (_viewModel.CanToggleReminderPause)
            _viewModel.SetRemindersPaused(!_viewModel.AreRemindersPaused);
    }

    private void UpdateMenuState()
    {
        _pauseItem.Text = _viewModel.ReminderPauseActionLabel;
        _pauseItem.Visible = _viewModel.Settings.RemindersEnabled;
        _pauseItem.Enabled = _viewModel.CanToggleReminderPause;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }

    private sealed class WaterlineMenuRenderer : Forms.ToolStripProfessionalRenderer
    {
        public WaterlineMenuRenderer() : base(new WaterlineColorTable()) => RoundedEdges = true;
    }

    private sealed class WaterlineColorTable : Forms.ProfessionalColorTable
    {
        private static readonly Color Canvas = Color.FromArgb(7, 21, 38);
        private static readonly Color Raised = Color.FromArgb(16, 43, 67);
        private static readonly Color Border = Color.FromArgb(50, 103, 127);
        public override Color ToolStripDropDownBackground => Canvas;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Border;
        public override Color MenuItemSelected => Raised;
        public override Color MenuItemSelectedGradientBegin => Raised;
        public override Color MenuItemSelectedGradientEnd => Raised;
        public override Color MenuItemPressedGradientBegin => Raised;
        public override Color MenuItemPressedGradientEnd => Raised;
        public override Color SeparatorDark => Color.FromArgb(24, 52, 71);
        public override Color SeparatorLight => Color.FromArgb(24, 52, 71);
    }
}
