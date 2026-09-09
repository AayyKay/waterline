using System.Windows;

namespace Waterline;

public enum UnsavedChoice { KeepEditing, Discard, Save }

public partial class UnsavedChangesDialog : Window
{
    public UnsavedChangesDialog(string destination)
    {
        InitializeComponent();
        PromptTitle.Text = $"Save {destination} changes?";
        PromptBody.Text = $"Your {destination.ToLowerInvariant()} draft has not been saved. Save it before leaving this destination?";
    }

    public UnsavedChoice Choice { get; private set; } = UnsavedChoice.KeepEditing;

    private void KeepEditing_Click(object sender, RoutedEventArgs e) { Choice = UnsavedChoice.KeepEditing; DialogResult = false; }
    private void Discard_Click(object sender, RoutedEventArgs e) { Choice = UnsavedChoice.Discard; DialogResult = true; }
    private void Save_Click(object sender, RoutedEventArgs e) { Choice = UnsavedChoice.Save; DialogResult = true; }
}
