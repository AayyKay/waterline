using System.Windows;
using System.Windows.Controls;

namespace Waterline;

public partial class GoalsView : System.Windows.Controls.UserControl
{
    public GoalsView() => InitializeComponent();
    private void Save_Click(object sender, RoutedEventArgs e) => (DataContext as MainViewModel)?.Configuration.SaveSettings();
    private void Cancel_Click(object sender, RoutedEventArgs e) => (DataContext as MainViewModel)?.Configuration.CancelSettings();
}
