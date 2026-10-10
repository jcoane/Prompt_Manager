using System.Windows;
using System.Windows.Controls;
using PromptManager.Models;

namespace PromptManager.Dialogs;

public sealed class FolderPickerDialog : Window
{
    private readonly ComboBox _folders;
    public int? FolderId => (_folders.SelectedItem as FolderOption)?.Id is int id && id != FolderOption.UnfiledId ? id : null;

    public FolderPickerDialog(string title, IEnumerable<FolderOption> folders, int? selectedId)
    {
        Title = title; Width = 550; Height = 205; MinWidth = 420; MinHeight = 205;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Icon = Application.Current?.MainWindow?.Icon;
        Background = (System.Windows.Media.Brush)Application.Current!.FindResource("PanelBrush");
        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = "Destination", FontWeight = FontWeights.SemiBold });
        _folders = new ComboBox { ItemsSource = folders.ToArray(), DisplayMemberPath = "Path", SelectedValuePath = "Id",
            SelectedValue = selectedId ?? FolderOption.UnfiledId, Margin = new Thickness(0, 10, 0, 15), VerticalAlignment = VerticalAlignment.Top };
        Grid.SetRow(_folders, 1); grid.Children.Add(_folders);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Padding = new Thickness(10, 5, 10, 5) };
        var move = new Button { Content = "Move", IsDefault = true, MinWidth = 80, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(10, 5, 10, 5) };
        move.Click += (_, _) => { if (_folders.SelectedItem != null) DialogResult = true; };
        buttons.Children.Add(cancel); buttons.Children.Add(move); Grid.SetRow(buttons, 2); grid.Children.Add(buttons); Content = grid;
    }
}
