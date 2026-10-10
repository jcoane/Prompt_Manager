using System.Windows;
using System.Windows.Controls;
using PromptManager.Services;

namespace PromptManager.Dialogs;

public sealed class VariableDialog : Window
{
    public VariableDialog(string body)
    {
        Title = "Fill Variables"; Width = 950; Height = 680; MinWidth = 660; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Icon = Application.Current.MainWindow?.Icon;
        Background = (System.Windows.Media.Brush)Application.Current.FindResource("PanelBrush");
        var grid = new Grid { Margin = new Thickness(20) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260), MinWidth = 180 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var fields = new StackPanel { Margin = new Thickness(0, 0, 16, 12) };
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var preview = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Text = body, FontSize = 15, Margin = new Thickness(0, 0, 0, 12) };
        var copy = new Button { Content = "Copy", MinWidth = 90, Padding = new Thickness(12, 6, 12, 6) };
        foreach (var name in VariableRenderer.Names(body))
        {
            values[name] = "";
            fields.Children.Add(new TextBlock { Text = name, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold });
            var input = new TextBox { Margin = new Thickness(0, 4, 0, 14), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 35, MaxHeight = 120, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            input.TextChanged += (_, _) => { values[name] = input.Text; preview.Text = VariableRenderer.Render(body, values); copy.IsEnabled = values.Values.All(value => !string.IsNullOrWhiteSpace(value)); };
            fields.Children.Add(input);
        }
        copy.IsEnabled = values.Count == 0;
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(preview.Text); DialogResult = true; }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
        };
        grid.Children.Add(new ScrollViewer { Content = fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Grid.SetColumn(preview, 1); grid.Children.Add(preview);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(new Button { Content = "Close", IsCancel = true, MinWidth = 90, Margin = new Thickness(0, 0, 8, 0) });
        buttons.Children.Add(copy); Grid.SetRow(buttons, 1); Grid.SetColumnSpan(buttons, 2); grid.Children.Add(buttons); Content = grid;
    }
}
