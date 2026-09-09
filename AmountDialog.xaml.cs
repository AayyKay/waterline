using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Waterline.Core;

namespace Waterline;

public partial class AmountDialog : Window
{
    public AmountDialog() => InitializeComponent();
    public double AmountOz { get; private set; }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        AmountBox.Focus();
        AmountBox.SelectAll();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetAmount(out var amount))
        {
            ShowError();
            return;
        }
        AmountOz = Math.Round(amount, 1);
        DialogResult = true;
    }

    private void AmountBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!IsInitialized || ErrorPanel is null) return;
        ErrorPanel.Visibility = Visibility.Hidden;
        AmountBox.Tag = null;
        AmountBox.ClearValue(System.Windows.Controls.Control.BorderBrushProperty);
        AmountBox.ClearValue(System.Windows.Controls.Control.BorderThicknessProperty);
    }

    private bool TryGetAmount(out double amount) =>
        double.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out amount) &&
        StateValidator.IsValidAmount(amount);

    private void ShowError()
    {
        ErrorPanel.Visibility = Visibility.Visible;
        AmountBox.Tag = "Invalid";
        AmountBox.BorderBrush = (System.Windows.Media.Brush)FindResource("ErrorBrush");
        AmountBox.BorderThickness = new Thickness(2);
        AmountBox.Focus();
        AmountBox.SelectAll();
    }

    public void PrepareInvalidForSnapshot()
    {
        AmountBox.Text = "700";
        ShowError();
    }
}
