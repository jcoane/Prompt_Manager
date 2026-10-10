using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using PromptManager.Dialogs;
using PromptManager.Models;
using PromptManager.ViewModels;
using PromptManager.Services;

namespace PromptManager;

public partial class MainWindow : Window
{
    private MainViewModel? _subscribedViewModel;
    private bool _isLoadingNotes;
    private string _loadedNotes = "";
    private string _loadedRtf = "";
    private string _normalizedRtf = "";
    private readonly string _settingsPath;

    public MainWindow() : this(UiSettings.DefaultPath) { }

    public MainWindow(string settingsPath)
    {
        _settingsPath = settingsPath;
        InitializeComponent();
        DataContextChanged += MainWindow_DataContextChanged;
        NotesColorComboBox.SelectedIndex = 0;
        Loaded += (_, _) => LoadSettings();
        SizeChanged += (_, _) => FitPanes();
    }

    private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= MainViewModel_PropertyChanged;
        }

        _subscribedViewModel = DataContext as MainViewModel;
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged += MainViewModel_PropertyChanged;
        }

        LoadSelectedPromptNotes();
    }

    private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedPrompt))
        {
            LoadSelectedPromptNotes();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && !viewModel.ConfirmClose())
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
        if (!e.Cancel) SaveSettings();
    }

    private void UserManualMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var manualPath = Path.Combine(AppContext.BaseDirectory, "USER_MANUAL.html");
        if (!File.Exists(manualPath)) manualPath = Path.Combine(AppContext.BaseDirectory, "USER_MANUAL.md");
        if (!File.Exists(manualPath))
        {
            MessageBox.Show(this, "The user manual file was not found: " + manualPath, "Prompt Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(manualPath)
            {
                UseShellExecute = true
            });
        }
        catch
        {
            Process.Start(new ProcessStartInfo("notepad.exe", "\"" + manualPath + "\"")
            {
                UseShellExecute = true
            });
        }
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow
        {
            Owner = this
        };
        aboutWindow.ShowDialog();
    }

    private void RecoveryRecords_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        new Window { Title="Library Recovery Records", Owner=this, Icon=Icon, Width=900, Height=650, MinWidth=550, MinHeight=350,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,
            Content=new TextBox { Text=vm.GetRecoveryReport(), IsReadOnly=true, AcceptsReturn=true, TextWrapping=TextWrapping.Wrap,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto, FontFamily=new FontFamily("Consolas"), Margin=new Thickness(16) } }.ShowDialog();
    }

    private void NewFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.NewFolderCommand.CanExecute(null))
        {
            viewModel.NewFolderCommand.Execute(null);
        }
    }

    private void DeleteFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.DeleteFolderCommand.CanExecute(null))
        {
            viewModel.DeleteFolderCommand.Execute(null);
        }
    }

    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            var folder = e.NewValue as FolderNode;
            viewModel.SelectedFolder = folder?.Id == 0 ? null : folder;
        }
    }

    private void PromptGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel { HasSelection: true }) { EditorTabs.SelectedIndex = 2; PromptBodyTextBox.Focus(); }
    }

    private void InsertVariableButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { SelectedPrompt: not null })
        {
            return;
        }

        var dialog = new TextInputDialog("Insert Variable", "Variable name", "Input")
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var variableName = Regex.Replace(dialog.Value.Trim(), @"\s+", "");
        if (!Regex.IsMatch(variableName, @"^[A-Za-z_][A-Za-z0-9_.-]*$"))
        {
            MessageBox.Show(this, "Use a letter or underscore first, followed by letters, numbers, underscores, dots, or hyphens.", "Variable Name", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var variableText = "{{" + variableName + "}}";
        var caret = PromptBodyTextBox.SelectionStart;
        PromptBodyTextBox.Text = PromptBodyTextBox.Text.Insert(caret, variableText);
        PromptBodyTextBox.SelectionStart = caret + variableText.Length;
        PromptBodyTextBox.Focus();
    }

    private void NotesBoldButton_Click(object sender, RoutedEventArgs e)
    {
        EditingCommands.ToggleBold.Execute(null, PromptNotesEditor);
        PromptNotesEditor.Focus();
    }

    private void NotesItalicButton_Click(object sender, RoutedEventArgs e)
    {
        EditingCommands.ToggleItalic.Execute(null, PromptNotesEditor);
        PromptNotesEditor.Focus();
    }

    private void NotesColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingNotes || !IsLoaded || DataContext is not MainViewModel { SelectedPrompt: not null })
        {
            return;
        }

        if (NotesColorComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string colorValue)
        {
            return;
        }

        var color = (Color)ColorConverter.ConvertFromString(colorValue);
        PromptNotesEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(color));
        UpdateSelectedPromptNotes();
        PromptNotesEditor.Focus();
    }

    private void PromptNotesEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingNotes)
        {
            return;
        }

        UpdateSelectedPromptNotes();
    }

    private void LoadSelectedPromptNotes()
    {
        _isLoadingNotes = true;
        PromptNotesEditor.Document.Blocks.Clear();

        if (DataContext is MainViewModel { SelectedPrompt: { } prompt })
        {
            if (string.IsNullOrWhiteSpace(prompt.FormattedNotes) || !TryLoadRtf(prompt.FormattedNotes))
                PromptNotesEditor.Document.Blocks.Add(new Paragraph(new Run(prompt.Notes ?? "")));
            _loadedNotes = prompt.Notes ?? "";
            _loadedRtf = prompt.FormattedNotes;
        }
        var range = new TextRange(PromptNotesEditor.Document.ContentStart, PromptNotesEditor.Document.ContentEnd);
        using var baseline = new MemoryStream();
        range.Save(baseline, DataFormats.Rtf);
        _normalizedRtf = Encoding.UTF8.GetString(baseline.ToArray());
        _isLoadingNotes = false;
    }

    private bool TryLoadRtf(string rtf)
    {
        try
        {
            var range = new TextRange(PromptNotesEditor.Document.ContentStart, PromptNotesEditor.Document.ContentEnd);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(rtf));
            range.Load(stream, DataFormats.Rtf);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void UpdateSelectedPromptNotes()
    {
        if (DataContext is not MainViewModel { SelectedPrompt: { } prompt })
        {
            return;
        }

        var range = new TextRange(PromptNotesEditor.Document.ContentStart, PromptNotesEditor.Document.ContentEnd);
        using var stream = new MemoryStream();
        range.Save(stream, DataFormats.Rtf);
        var rtf = Encoding.UTF8.GetString(stream.ToArray());
        // WPF may normalize RTF during load; opening a record must not make it dirty.
        prompt.Notes = rtf == _normalizedRtf ? _loadedNotes : range.Text.TrimEnd('\r', '\n');
        prompt.FormattedNotes = rtf == _normalizedRtf ? _loadedRtf : rtf;
    }

    private void PromptGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.SelectedPromptIds.Clear();
        viewModel.SelectedPromptIds.AddRange(PromptGrid.SelectedItems.OfType<PromptRow>().Select(prompt => prompt.Id));
        viewModel.MoveSelectedCommand.RaiseCanExecuteChanged();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control || DataContext is not MainViewModel vm) return;
        if (e.Key == Key.F) { SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; }
        if (e.Key == Key.S && vm.SaveCommand.CanExecute(null)) { vm.SaveCommand.Execute(null); e.Handled = true; }
        if (e.Key == Key.N) { vm.NewPromptCommand.Execute(null); e.Handled = true; }
    }

    private void ExpandNotes_Click(object sender, RoutedEventArgs e) => ExpandEditor(1, "Notes");
    private void ExpandPrompt_Click(object sender, RoutedEventArgs e) => ExpandEditor(2, "Prompt");

    private void ExpandEditor(int index, string name)
    {
        var tab = (TabItem)EditorTabs.Items[index];
        var editor = (Grid)tab.Content;
        tab.Content = null;
        var window = new Window { Title = name + " - " + (DataContext as MainViewModel)?.SelectedPrompt?.Title,
            Width = Math.Min(1100, SystemParameters.WorkArea.Width), Height = Math.Min(780, SystemParameters.WorkArea.Height),
            MinWidth = 600, MinHeight = 400, Owner = this, Icon = Icon, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            DataContext = DataContext, Background = Background };
        var layout = new DockPanel();
        var done = new Button { Content = "Done", MinWidth = 90, Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12), IsCancel = true };
        done.Click += (_, _) => window.Close();
        DockPanel.SetDock(done, Dock.Bottom); layout.Children.Add(done); layout.Children.Add(editor); window.Content = layout;
        try { window.ShowDialog(); }
        finally { layout.Children.Remove(editor); tab.Content = editor; }
    }

    private void EditorFontSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PromptNotesEditor == null || PromptBodyTextBox == null || EditorFontSize.SelectedItem is not ComboBoxItem item
            || !int.TryParse(item.Content?.ToString(), out var size)) return;
        PromptNotesEditor.LayoutTransform = new ScaleTransform(size / 16.0, size / 16.0);
        PromptBodyTextBox.FontSize = size;
    }

    private void LoadSettings()
    {
        var settings = UiSettings.Load(_settingsPath);
        NavigationColumn.Width = new GridLength(double.IsFinite(settings.NavigationWidth) ? Math.Clamp(settings.NavigationWidth, 180, 320) : 220);
        EditorColumn.Width = new GridLength(double.IsFinite(settings.EditorWidth) ? Math.Clamp(settings.EditorWidth, 350, Math.Max(350, ActualWidth - 550)) : 500);
        foreach (var setting in (settings.Columns ?? new()).OrderBy(column => column.Order))
        {
            var column = PromptGrid.Columns.FirstOrDefault(column => column.Header.ToString() == setting.Name);
            if (column == null) continue;
            column.Visibility = setting.Visible ? Visibility.Visible : Visibility.Collapsed;
            if (double.IsFinite(setting.Width)) column.Width = Math.Clamp(setting.Width, column.MinWidth, 1200);
            column.DisplayIndex = Math.Clamp(setting.Order, 0, PromptGrid.Columns.Count - 1);
        }
        PromptGrid.Columns[0].Visibility = Visibility.Visible;
        foreach (var column in PromptGrid.Columns)
        {
            var menuItem = new MenuItem { Header = column.Header, IsCheckable = true, IsChecked = column.Visibility == Visibility.Visible,
                IsEnabled = column != PromptGrid.Columns[0] };
            menuItem.Click += (_, _) => column.Visibility = menuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;
            ColumnsMenu.Items.Add(menuItem);
        }
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(PromptGrid.ItemsSource);
        var properties = typeof(PromptRow).GetProperties().Select(property => property.Name).ToHashSet();
        foreach (var sort in settings.Sort ?? new())
            if (properties.Contains(sort.Property)) view.SortDescriptions.Add(new SortDescription(sort.Property, sort.Descending ? ListSortDirection.Descending : ListSortDirection.Ascending));
        foreach (ComboBoxItem item in EditorFontSize.Items)
            if (item.Content.ToString() == settings.ReadingSize.ToString()) EditorFontSize.SelectedItem = item;
        EditorFontSize_SelectionChanged(EditorFontSize, new SelectionChangedEventArgs(ComboBox.SelectionChangedEvent, Array.Empty<object>(), Array.Empty<object>()));
    }

    private void SaveSettings()
    {
        var settings = new UiSettings { NavigationWidth = NavigationColumn.ActualWidth, EditorWidth = EditorColumn.ActualWidth,
            ReadingSize = int.Parse(((ComboBoxItem)EditorFontSize.SelectedItem).Content.ToString()!) };
        settings.Columns.AddRange(PromptGrid.Columns.Select(column => new ColumnSetting(column.Header.ToString()!, column.Visibility == Visibility.Visible, column.ActualWidth, column.DisplayIndex)));
        settings.Sort.AddRange(System.Windows.Data.CollectionViewSource.GetDefaultView(PromptGrid.ItemsSource).SortDescriptions.Select(sort => new SortSetting(sort.PropertyName, sort.Direction == ListSortDirection.Descending)));
        try { settings.Save(_settingsPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { System.Diagnostics.Debug.WriteLine("Unable to save view preferences: " + ex.Message); }
    }

    private void FitPanes()
    {
        var width = (Content as FrameworkElement)?.ActualWidth ?? 0;
        if (width <= 0) width = ActualWidth - 16;
        if (width <= 0) return;
        NavigationColumn.Width = new GridLength(Math.Clamp(NavigationColumn.Width.Value, 180, Math.Max(180, width - 710)));
        EditorColumn.Width = new GridLength(Math.Clamp(EditorColumn.Width.Value, 350, Math.Max(350, width - NavigationColumn.Width.Value - 360)));
    }
}

