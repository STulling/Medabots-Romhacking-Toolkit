using System.Windows;

namespace Medabots.Rom.WPFEditor.Dialogs;

public partial class InputDialog : Window
{
    public InputDialog(string title, string prompt, string acceptLabel, string cancelLabel, string? placeholder, string? initialValue)
    {
        InitializeComponent();
        Title = title;
        PromptTextBlock.Text = prompt;
        PlaceholderTextBlock.Text = string.IsNullOrWhiteSpace(placeholder) ? string.Empty : $"Hint: {placeholder}";
        PlaceholderTextBlock.Visibility = string.IsNullOrWhiteSpace(placeholder) ? Visibility.Collapsed : Visibility.Visible;
        OkButton.Content = acceptLabel;
        ResponseTextBox.Text = initialValue ?? string.Empty;
        Loaded += (_, _) =>
        {
            ResponseTextBox.Focus();
            ResponseTextBox.SelectAll();
        };
    }

    public string ResponseText => ResponseTextBox.Text;

    private void OnOkClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}