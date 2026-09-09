using System.Windows;
using System.Windows.Controls;

namespace Waterline;

public partial class WidgetView : System.Windows.Controls.UserControl
{
    public WidgetView() => InitializeComponent();
    public event EventHandler? OpenRequested;
    private void Open_Click(object sender, RoutedEventArgs e) => OpenRequested?.Invoke(this, EventArgs.Empty);
}
