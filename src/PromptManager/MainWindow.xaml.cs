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

namespace PromptManager;

public partial class MainWindow : Window
{
    private MainViewModel? _subscribedViewModel;
    private bool _isLoadingNotes;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += MainWindow_DataContextChanged;
        NotesColorComboBox.SelectedIndex = 0;
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
    }

    private void UserManualMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var manualPath = Path.Combine(AppContext.BaseDirectory, "USER_MANUAL.md");
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
        if (DataContext is MainViewModel viewModel && viewModel.CopyCommand.CanExecute(null))
        {
            viewModel.CopyCommand.Execute(null);
        }
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
        if (string.IsNullOrWhiteSpace(variableName))
        {
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
            if (!string.IsNullOrWhiteSpace(prompt.FormattedNotes) && TryLoadRtf(prompt.FormattedNotes))
            {
                _isLoadingNotes = false;
                return;
            }

            PromptNotesEditor.Document.Blocks.Add(new Paragraph(new Run(prompt.Notes ?? "")));
        }

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
        prompt.Notes = range.Text.TrimEnd('\r', '\n');

        using var stream = new MemoryStream();
        range.Save(stream, DataFormats.Rtf);
        prompt.FormattedNotes = Encoding.UTF8.GetString(stream.ToArray());
    }
}

