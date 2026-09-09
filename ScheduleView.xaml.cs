using System.Windows;
using System.Windows.Controls;

namespace Waterline;

public partial class ScheduleView : System.Windows.Controls.UserControl
{
    public ScheduleView() => InitializeComponent();
    private void Save_Click(object sender, RoutedEventArgs e) => (DataContext as MainViewModel)?.Configuration.SaveSchedule();
    private void Cancel_Click(object sender, RoutedEventArgs e) => (DataContext as MainViewModel)?.Configuration.CancelSchedule();
}
