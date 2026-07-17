using System.Windows;

namespace PromptManager.Dialogs;

public partial class TextInputDialog : Window
{
    public TextInputDialog(string title, string prompt, string initialValue)
    {
        InitializeComponent();
        Title = title;
        PromptTextBlock.Text = prompt;
        ValueTextBox.Text = initialValue;
        ValueTextBox.SelectAll();
        ValueTextBox.Focus();
    }

    public string Value => ValueTextBox.Text.Trim();

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Value))
        {
            MessageBox.Show(this, "Enter a value before continuing.", "Prompt Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }
}

