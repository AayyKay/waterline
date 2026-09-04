using System.Globalization;
using System.Windows;

namespace Waterline;

public partial class AmountDialog : Window
{
    public AmountDialog() => InitializeComponent();
    public double AmountOz { get; private set; }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount is <= 0 or > 64)
        {
            System.Windows.MessageBox.Show(this, "Enter an amount between 0.1 and 64 oz.", "Check amount", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        AmountOz = Math.Round(amount, 1);
        DialogResult = true;
    }
}
