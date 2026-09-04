using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Waterline;

public partial class WidgetWindow : Window
{
    private static WidgetWindow? _current;
    private readonly MainViewModel _viewModel;

    public WidgetWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _current = this;
        Loaded += (_, _) => PositionBottomRight();
        Closed += (_, _) => { if (ReferenceEquals(_current, this)) _current = null; };
    }

    public static void ShowOrActivate(MainViewModel viewModel)
    {
        if (_current is { IsVisible: true }) { _current.Activate(); return; }
        new WidgetWindow(viewModel).Show();
    }

    private void PositionBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 20;
        Top = area.Bottom - Height - 20;
    }

    private void Add8_Click(object sender, RoutedEventArgs e) => _viewModel.AddDrink(8);
    private void Add12_Click(object sender, RoutedEventArgs e) => _viewModel.AddDrink(12);
    private void Add16_Click(object sender, RoutedEventArgs e) => _viewModel.AddDrink(16);
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void OpenDashboard_Click(object sender, MouseButtonEventArgs e)
    {
        if (System.Windows.Application.Current.MainWindow is MainWindow main)
        {
            main.Show();
            main.Activate();
        }
    }

    private void Custom_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AmountDialog { Owner = this };
        if (dialog.ShowDialog() == true) _viewModel.AddDrink(dialog.AmountOz);
    }

    private void Collapse_Click(object sender, RoutedEventArgs e)
        => SetCollapsedForSnapshot();

    public void SetCollapsedForSnapshot()
    {
        ExpandedCard.Visibility = Visibility.Collapsed;
        CollapsedOrb.Visibility = Visibility.Visible;
        MinWidth = MinHeight = 0;
        Width = Height = 118;
        PositionBottomRight();
    }

    private void Expand_Click(object sender, RoutedEventArgs e)
    {
        CollapsedOrb.Visibility = Visibility.Collapsed;
        ExpandedCard.Visibility = Visibility.Visible;
        MinWidth = 410;
        MinHeight = 490;
        Width = 410;
        Height = 490;
        PositionBottomRight();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
