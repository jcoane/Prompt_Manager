using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using PromptManager.Data;
using PromptManager.Dialogs;
using PromptManager.Models;
using PromptManager.Services;

namespace PromptManager.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly PromptRepository _repository;
    private readonly PromptFileService _fileService;
    private PromptRow? _subscribedPrompt;
    private string _searchText = "";
    private FolderNode? _selectedFolder;
    private PromptRow? _selectedPrompt;
    private int? _selectedPromptOriginalFolderId;
    private int _selectedPromptFolderOptionId = FolderOption.UnfiledId;
    private FilterItem? _selectedFilter;
    private string _changeNote = "";
    private string? _selectedLabelToAdd;
    private string _versionComparisonText = "";
    private string _resultSummary = "";
    private string _statusMessage = "Ready.";

    public MainViewModel(PromptRepository repository, PromptFileService fileService)
    {
        _repository = repository;
        _fileService = fileService;

        Filters.Add(new FilterItem(PromptFilterKind.All, "All Prompts"));
        Filters.Add(new FilterItem(PromptFilterKind.Favorites, "Favorites"));
        Filters.Add(new FilterItem(PromptFilterKind.RecentlyUsed, "Recently Used"));
        Filters.Add(new FilterItem(PromptFilterKind.Drafts, "Drafts"));
        Filters.Add(new FilterItem(PromptFilterKind.Approved, "Approved"));
        Filters.Add(new FilterItem(PromptFilterKind.Retired, "Retired"));
        _selectedFilter = Filters[0];

        RefreshCommand = new RelayCommand(RefreshAll);
        ClearSearchCommand = new RelayCommand(ClearSearch);
        NewFolderCommand = new RelayCommand(CreateFolder);
        DeleteFolderCommand = new RelayCommand(DeleteSelectedFolder, () => SelectedFolder is not null);
        NewPromptCommand = new RelayCommand(CreatePrompt);
        SaveCommand = new RelayCommand(SaveSelectedPrompt, () => HasSelection);
        AddLabelCommand = new RelayCommand(AddSelectedLabel, () => HasSelection && !string.IsNullOrWhiteSpace(SelectedLabelToAdd));
        ApplyModeTemplateCommand = new RelayCommand(ApplyModeTemplate, () => HasSelection);
        CopyCommand = new RelayCommand(CopySelectedPrompt, () => HasSelection);
        ToggleFavoriteCommand = new RelayCommand(ToggleFavorite, () => HasSelection);
        ArchiveCommand = new RelayCommand(ArchiveSelectedPrompt, () => HasSelection);
        RestorePromptCommand = new RelayCommand(RestoreSelectedPrompt, () => HasSelection);
        DeleteCommand = new RelayCommand(DeleteSelectedPrompt, () => HasSelection);
        ImportCommand = new RelayCommand(ImportPrompts);
        ExportCommand = new RelayCommand(ExportPrompts);
        BackupCommand = new RelayCommand(BackupDatabase);
        RestoreCommand = new RelayCommand(RestoreDatabase);

        RefreshAll();
    }

    public ObservableCollection<FilterItem> Filters { get; } = new();

    public ObservableCollection<FolderNode> FolderTree { get; } = new();

    public ObservableCollection<FolderOption> FolderOptions { get; } = new();

    public ObservableCollection<string> TypeOptions { get; } = new();

    public ObservableCollection<string> LabelOptions { get; } = new();

    public ObservableCollection<string> PromptVariables { get; } = new();

    public ObservableCollection<string> QualityIssues { get; } = new();

    public ObservableCollection<string> DuplicateWarnings { get; } = new();

    public ObservableCollection<PromptRow> Prompts { get; } = new();

    public IReadOnlyList<PromptStatus> Statuses { get; } = Enum.GetValues<PromptStatus>();

    public IReadOnlyList<string> ModeOptions { get; } = new[]
    {
        "Text",
        "Chat",
        "System/User",
        "Checklist",
        "Matrix",
        "Protocol"
    };

    public RelayCommand RefreshCommand { get; }

    public RelayCommand ClearSearchCommand { get; }

    public RelayCommand NewFolderCommand { get; }

    public RelayCommand DeleteFolderCommand { get; }

    public RelayCommand NewPromptCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand AddLabelCommand { get; }

    public RelayCommand ApplyModeTemplateCommand { get; }

    public RelayCommand CopyCommand { get; }

    public RelayCommand ToggleFavoriteCommand { get; }

    public RelayCommand ArchiveCommand { get; }

    public RelayCommand RestorePromptCommand { get; }

    public RelayCommand DeleteCommand { get; }

    public RelayCommand ImportCommand { get; }

    public RelayCommand ExportCommand { get; }

    public RelayCommand BackupCommand { get; }

    public RelayCommand RestoreCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                TryRefreshPrompts();
            }
        }
    }

    public FilterItem? SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                TryRefreshPrompts();
                OnPropertyChanged(nameof(RegisterTitle));
            }
        }
    }

    public FolderNode? SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (SetProperty(ref _selectedFolder, value))
            {
                TryRefreshPrompts();
                OnPropertyChanged(nameof(RegisterTitle));
                OnPropertyChanged(nameof(HasSelectedFolder));
                DeleteFolderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public PromptRow? SelectedPrompt
    {
        get => _selectedPrompt;
        set
        {
            if (SetProperty(ref _selectedPrompt, value))
            {
                if (_subscribedPrompt is not null)
                {
                    _subscribedPrompt.PropertyChanged -= SelectedPrompt_PropertyChanged;
                }

                _subscribedPrompt = value;
                if (_subscribedPrompt is not null)
                {
                    _subscribedPrompt.PropertyChanged += SelectedPrompt_PropertyChanged;
                }

                OnPropertyChanged(nameof(HasSelection));
                SaveCommand.RaiseCanExecuteChanged();
                AddLabelCommand.RaiseCanExecuteChanged();
                ApplyModeTemplateCommand.RaiseCanExecuteChanged();
                CopyCommand.RaiseCanExecuteChanged();
                ToggleFavoriteCommand.RaiseCanExecuteChanged();
                ArchiveCommand.RaiseCanExecuteChanged();
                RestorePromptCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
                _selectedPromptOriginalFolderId = value?.FolderId;
                _selectedPromptFolderOptionId = value?.FolderId ?? FolderOption.UnfiledId;
                OnPropertyChanged(nameof(SelectedPromptFolderOptionId));
                ChangeNote = "";
                RefreshSelectedPromptAnalysis();
            }
        }
    }

    public bool HasSelection => SelectedPrompt is not null;

    public bool HasSelectedFolder => SelectedFolder is not null;

    public int SelectedPromptFolderOptionId
    {
        get => _selectedPromptFolderOptionId;
        set
        {
            if (SetProperty(ref _selectedPromptFolderOptionId, value) && SelectedPrompt is not null)
            {
                SelectedPrompt.FolderId = value == FolderOption.UnfiledId ? null : value;
            }
        }
    }

    public string ChangeNote
    {
        get => _changeNote;
        set => SetProperty(ref _changeNote, value);
    }

    public string? SelectedLabelToAdd
    {
        get => _selectedLabelToAdd;
        set
        {
            if (SetProperty(ref _selectedLabelToAdd, value))
            {
                AddLabelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string VersionComparisonText
    {
        get => _versionComparisonText;
        private set => SetProperty(ref _versionComparisonText, value);
    }

    public string RegisterTitle
    {
        get
        {
            var filterName = SelectedFilter?.Name ?? "All Prompts";
            return SelectedFolder is null ? filterName : filterName + " - " + SelectedFolder.Name;
        }
    }

    public string ResultSummary
    {
        get => _resultSummary;
        private set => SetProperty(ref _resultSummary, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string DatabasePath => _repository.DatabasePath;

    private void RefreshAll()
    {
        try
        {
            RefreshFolders();
            RefreshTypes();
            RefreshLabels();
            RefreshCounts();
            RefreshPrompts(SelectedPrompt?.Id);
            StatusMessage = "Library refreshed.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Prompt Manager", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshFolders(int? selectedFolderId = null, bool preserveCurrentSelection = true)
    {
        var targetFolderId = preserveCurrentSelection ? selectedFolderId ?? SelectedFolder?.Id : selectedFolderId;
        var folders = _repository.LoadFolderTree();

        RefreshFolderOptions(folders);

        FolderTree.Clear();
        var allFolders = new FolderNode(0, null, "All Folders")
        {
            IsSelected = !targetFolderId.HasValue
        };
        FolderTree.Add(allFolders);

        foreach (var folder in folders)
        {
            FolderTree.Add(folder);
        }

        if (!targetFolderId.HasValue)
        {
            SelectedFolder = null;
            return;
        }

        if (TrySelectFolder(FolderTree, targetFolderId.Value, out var selectedFolder))
        {
            SelectedFolder = selectedFolder;
            return;
        }

        allFolders.IsSelected = true;
        SelectedFolder = null;
    }

    private void RefreshFolderOptions(IEnumerable<FolderNode> folders)
    {
        FolderOptions.Clear();
        FolderOptions.Add(new FolderOption(FolderOption.UnfiledId, "Unfiled"));

        foreach (var folder in folders)
        {
            AddFolderOption(folder, "");
        }
    }

    private void RefreshTypes()
    {
        var currentType = SelectedPrompt?.Type;
        TypeOptions.Clear();

        foreach (var type in _repository.GetPromptTypes())
        {
            TypeOptions.Add(type);
        }

        if (!string.IsNullOrWhiteSpace(currentType) && !TypeOptions.Contains(currentType))
        {
            TypeOptions.Add(currentType);
        }
    }

    private void RefreshLabels()
    {
        LabelOptions.Clear();

        foreach (var label in _repository.GetPromptLabels())
        {
            LabelOptions.Add(label);
        }
    }

    private void AddFolderOption(FolderNode folder, string parentPath)
    {
        var path = string.IsNullOrWhiteSpace(parentPath) ? folder.Name : parentPath + " / " + folder.Name;
        FolderOptions.Add(new FolderOption(folder.Id, path));

        foreach (var child in folder.Children)
        {
            AddFolderOption(child, path);
        }
    }

    private static bool TrySelectFolder(IEnumerable<FolderNode> folders, int folderId, out FolderNode? selectedFolder)
    {
        foreach (var folder in folders)
        {
            if (folder.Id == folderId)
            {
                folder.IsSelected = true;
                folder.IsExpanded = true;
                selectedFolder = folder;
                return true;
            }

            if (TrySelectFolder(folder.Children, folderId, out selectedFolder))
            {
                folder.IsExpanded = true;
                return true;
            }
        }

        selectedFolder = null;
        return false;
    }

    private void RefreshCounts()
    {
        var counts = _repository.GetFilterCounts();
        foreach (var filter in Filters)
        {
            filter.Count = counts.TryGetValue(filter.Kind, out var count) ? count : 0;
        }
    }

    private void TryRefreshPrompts(int? preferredPromptId = null)
    {
        try
        {
            RefreshPrompts(preferredPromptId);
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to refresh prompts: " + ex.Message;
            MessageBox.Show(ex.Message, "Prompt Manager", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshPrompts(int? preferredPromptId = null)
    {
        var filter = SelectedFilter?.Kind ?? PromptFilterKind.All;
        var previousId = preferredPromptId ?? SelectedPrompt?.Id;
        var rows = _repository.GetPrompts(filter, SelectedFolder?.Id, SearchText);

        Prompts.Clear();
        foreach (var row in rows)
        {
            Prompts.Add(row);
        }

        SelectedPrompt = previousId.HasValue
            ? Prompts.FirstOrDefault(prompt => prompt.Id == previousId.Value) ?? Prompts.FirstOrDefault()
            : Prompts.FirstOrDefault();

        ResultSummary = Prompts.Count == 1 ? "1 prompt" : Prompts.Count + " prompts";
        OnPropertyChanged(nameof(RegisterTitle));
    }

    private void ClearSearch()
    {
        if (string.IsNullOrEmpty(SearchText))
        {
            StatusMessage = "Search is already clear.";
            return;
        }

        SearchText = "";
        StatusMessage = "Search cleared.";
    }

    private void CreateFolder()
    {
        var dialog = new TextInputDialog("New Folder", "Folder name", "New Folder")
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var newFolderId = _repository.CreateFolder(SelectedFolder?.Id, dialog.Value);
            RefreshFolders(newFolderId);
            RefreshCounts();
            StatusMessage = "Folder created: " + dialog.Value;
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void DeleteSelectedFolder()
    {
        if (SelectedFolder is null)
        {
            return;
        }

        var folderName = SelectedFolder.Name;
        var parentFolderId = SelectedFolder.ParentId;
        var confirm = MessageBox.Show(
            "Delete folder '" + folderName + "' and all subfolders? Prompts in those folders will be moved to Unfiled and will not be deleted.",
            "Delete Folder",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _repository.DeleteFolder(SelectedFolder.Id);
            RefreshFolders(parentFolderId, preserveCurrentSelection: false);
            RefreshCounts();
            RefreshPrompts();
            StatusMessage = "Deleted folder " + folderName + ". Prompts were moved to Unfiled.";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void CreatePrompt()
    {
        try
        {
            SelectedFilter = Filters.First(filter => filter.Kind == PromptFilterKind.All);
            var promptId = _repository.CreatePrompt(SelectedFolder?.Id);
            RefreshCounts();
            RefreshPrompts(promptId);
            StatusMessage = "New draft prompt created.";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void SaveSelectedPrompt()
    {
        if (SelectedPrompt is null)
        {
            return;
        }

        try
        {
            var bodyChanged = !string.Equals(_repository.GetCurrentPromptBody(SelectedPrompt.Id), SelectedPrompt.Body, StringComparison.Ordinal);
            if (bodyChanged && string.IsNullOrWhiteSpace(ChangeNote))
            {
                StatusMessage = "Enter a change note before saving body text changes.";
                MessageBox.Show("Enter a change note before saving body text changes.", "Prompt Manager", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var promptMoved = _selectedPromptOriginalFolderId != SelectedPrompt.FolderId;
            var saved = _repository.SavePrompt(SelectedPrompt, ChangeNote);

            if (promptMoved)
            {
                RefreshFolders(saved.FolderId, preserveCurrentSelection: false);
            }

            RefreshTypes();
            RefreshLabels();
            RefreshCounts();
            RefreshPrompts(saved.Id);
            ChangeNote = "";
            StatusMessage = promptMoved
                ? "Saved " + saved.Title + " and moved it to " + saved.FolderPath + "."
                : "Saved " + saved.Title + ".";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void CopySelectedPrompt()
    {
        if (SelectedPrompt is null)
        {
            return;
        }

        try
        {
            Clipboard.SetText(SelectedPrompt.Body ?? "");
            _repository.MarkUsed(SelectedPrompt.Id);
            RefreshCounts();
            RefreshPrompts(SelectedPrompt.Id);
            StatusMessage = "Copied prompt text to the clipboard.";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ToggleFavorite()
    {
        if (SelectedPrompt is null)
        {
            return;
        }

        try
        {
            _repository.SetFavorite(SelectedPrompt.Id, !SelectedPrompt.IsFavorite);
            RefreshCounts();
            RefreshPrompts(SelectedPrompt.Id);
            StatusMessage = "Favorite flag updated.";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ArchiveSelectedPrompt()
    {
        if (SelectedPrompt is null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            "Archive this prompt and mark it retired?",
            "Archive Prompt",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _repository.ArchivePrompt(SelectedPrompt.Id);
            RefreshCounts();
            RefreshPrompts();
            StatusMessage = "Prompt archived.";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void RestoreSelectedPrompt()
    {
        if (SelectedPrompt is null)
        {
            return;
        }

        try
        {
            _repository.RestorePrompt(SelectedPrompt.Id);
            RefreshCounts();
            RefreshPrompts(SelectedPrompt.Id);
            StatusMessage = "Prompt restored as draft.";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void DeleteSelectedPrompt()
    {
        if (SelectedPrompt is null)
        {
            return;
        }

        var title = SelectedPrompt.Title;
        var confirm = MessageBox.Show(
            "Delete this prompt permanently? This will remove the prompt and its version history. Use Archive if you only want to retire it.",
            "Delete Prompt",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _repository.DeletePrompt(SelectedPrompt.Id);
            RefreshCounts();
            RefreshPrompts();
            StatusMessage = "Deleted " + title + ".";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ImportPrompts()
    {
        try
        {
            var imported = _fileService.Import();
            if (!imported.HasValue)
            {
                return;
            }

            RefreshAll();
            StatusMessage = imported.Value == 1 ? "Imported 1 prompt." : "Imported " + imported.Value + " prompts.";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ExportPrompts()
    {
        try
        {
            var exportedTo = _fileService.Export(SelectedPrompt?.Id);
            if (exportedTo is null)
            {
                return;
            }

            StatusMessage = "Exported to " + exportedTo;
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void BackupDatabase()
    {
        try
        {
            var backupPath = _fileService.Backup();
            if (backupPath is not null)
            {
                StatusMessage = "Backed up database to " + backupPath;
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void RestoreDatabase()
    {
        try
        {
            if (_fileService.Restore())
            {
                SelectedFolder = null;
                RefreshAll();
                StatusMessage = "Database restored.";
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void SelectedPrompt_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PromptRow.Title) or nameof(PromptRow.Body) or nameof(PromptRow.Tags) or nameof(PromptRow.Labels) or nameof(PromptRow.Mode) or nameof(PromptRow.Status))
        {
            RefreshSelectedPromptAnalysis();
        }
    }

    private void AddSelectedLabel()
    {
        if (SelectedPrompt is null || string.IsNullOrWhiteSpace(SelectedLabelToAdd))
        {
            return;
        }

        var labels = SplitLabels(SelectedPrompt.Labels).ToList();
        if (!labels.Contains(SelectedLabelToAdd, StringComparer.CurrentCultureIgnoreCase))
        {
            labels.Add(SelectedLabelToAdd.Trim());
            SelectedPrompt.Labels = string.Join("; ", labels);
        }
    }

    private void ApplyModeTemplate()
    {
        if (SelectedPrompt is null)
        {
            return;
        }

        var template = GetModeTemplate(SelectedPrompt.Mode);
        if (string.IsNullOrWhiteSpace(template))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedPrompt.Body))
        {
            SelectedPrompt.Body = template;
            return;
        }

        var confirm = MessageBox.Show(
            "Append the " + SelectedPrompt.Mode + " structure to the current prompt text?",
            "Apply Mode Template",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm == MessageBoxResult.Yes)
        {
            SelectedPrompt.Body = SelectedPrompt.Body.TrimEnd() + Environment.NewLine + Environment.NewLine + template;
        }
    }

    private void RefreshSelectedPromptAnalysis()
    {
        RefreshVariables();
        RefreshQualityIssues();
        RefreshDuplicateWarnings();
        RefreshVersionComparison();
    }

    private void RefreshVariables()
    {
        PromptVariables.Clear();

        if (SelectedPrompt is null)
        {
            return;
        }

        var variables = Regex.Matches(SelectedPrompt.Body ?? "", @"\{\{\s*([A-Za-z_][A-Za-z0-9_.-]*)\s*\}\}")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(variable => variable, StringComparer.CurrentCultureIgnoreCase);

        foreach (var variable in variables)
        {
            PromptVariables.Add(variable);
        }
    }

    private void RefreshQualityIssues()
    {
        QualityIssues.Clear();

        if (SelectedPrompt is null)
        {
            return;
        }

        var body = SelectedPrompt.Body ?? "";
        if (string.IsNullOrWhiteSpace(SelectedPrompt.Title) || SelectedPrompt.Title.StartsWith("Untitled", StringComparison.CurrentCultureIgnoreCase))
        {
            QualityIssues.Add("Use a specific title before review or approval.");
        }

        if (string.IsNullOrWhiteSpace(SelectedPrompt.Tags))
        {
            QualityIssues.Add("Add searchable tags.");
        }

        if (string.IsNullOrWhiteSpace(SelectedPrompt.Labels))
        {
            QualityIssues.Add("Add at least one label such as Production, Approved, Training, or Deprecated.");
        }

        if (body.Trim().Length < 50)
        {
            QualityIssues.Add("Prompt text is short; confirm it contains enough instruction context.");
        }

        if (CountOccurrences(body, "{{") != CountOccurrences(body, "}}"))
        {
            QualityIssues.Add("Variable braces appear unbalanced.");
        }

        if (body.Contains("TODO", StringComparison.CurrentCultureIgnoreCase) || body.Contains("TBD", StringComparison.CurrentCultureIgnoreCase))
        {
            QualityIssues.Add("Remove TODO/TBD placeholders before approval.");
        }

        AddModeQualityChecks(SelectedPrompt.Mode, body);

        if (SelectedPrompt.Status == PromptStatus.Approved && !SplitLabels(SelectedPrompt.Labels).Any(label => label.Equals("Approved", StringComparison.CurrentCultureIgnoreCase) || label.Equals("Production", StringComparison.CurrentCultureIgnoreCase)))
        {
            QualityIssues.Add("Approved prompts should carry an Approved or Production label.");
        }

        if (QualityIssues.Count == 0)
        {
            QualityIssues.Add("No review issues detected.");
        }
    }

    private void AddModeQualityChecks(string mode, string body)
    {
        var checks = GetRequiredSections(mode);
        foreach (var section in checks)
        {
            if (!body.Contains(section, StringComparison.CurrentCultureIgnoreCase))
            {
                QualityIssues.Add("Mode '" + mode + "' is missing section: " + section);
            }
        }
    }

    private void RefreshDuplicateWarnings()
    {
        DuplicateWarnings.Clear();

        if (SelectedPrompt is null)
        {
            return;
        }

        foreach (var warning in _repository.GetDuplicateWarnings(SelectedPrompt.Id, SelectedPrompt.Title, SelectedPrompt.Body))
        {
            DuplicateWarnings.Add(warning);
        }

        if (DuplicateWarnings.Count == 0)
        {
            DuplicateWarnings.Add("No duplicate title or body match detected.");
        }
    }

    private void RefreshVersionComparison()
    {
        if (SelectedPrompt is null)
        {
            VersionComparisonText = "";
            return;
        }

        var versions = _repository.GetPromptVersions(SelectedPrompt.Id).OrderBy(version => version.VersionNumber).ToList();
        if (versions.Count == 0)
        {
            VersionComparisonText = "No saved body versions yet.";
            return;
        }

        var latest = versions[^1];
        if (!string.Equals(latest.Body, SelectedPrompt.Body, StringComparison.Ordinal))
        {
            VersionComparisonText = BuildVersionComparison("Unsaved body changes compared with v" + latest.VersionNumber, latest.Body, SelectedPrompt.Body);
            return;
        }

        if (versions.Count == 1)
        {
            VersionComparisonText = "Only one saved body version.";
            return;
        }

        var previous = versions[^2];
        VersionComparisonText = BuildVersionComparison("Latest saved change v" + previous.VersionNumber + " -> v" + latest.VersionNumber + " (" + latest.ChangeNote + ")", previous.Body, latest.Body);
    }

    private static string BuildVersionComparison(string title, string oldBody, string newBody)
    {
        var oldLines = SplitLines(oldBody).ToList();
        var newLines = SplitLines(newBody).ToList();
        var added = newLines.Except(oldLines, StringComparer.Ordinal).Count();
        var removed = oldLines.Except(newLines, StringComparer.Ordinal).Count();
        return title + Environment.NewLine + "Added lines: " + added + Environment.NewLine + "Removed lines: " + removed;
    }

    private static IEnumerable<string> SplitLines(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n').Where(line => !string.IsNullOrWhiteSpace(line));
    }

    private static IReadOnlyList<string> GetRequiredSections(string mode)
    {
        return mode switch
        {
            "Chat" => new[] { "System", "User" },
            "System/User" => new[] { "System", "User" },
            "Checklist" => new[] { "Checklist", "Acceptance Criteria" },
            "Matrix" => new[] { "Rows", "Columns" },
            "Protocol" => new[] { "Purpose", "Scope", "Procedure", "Acceptance Criteria" },
            _ => Array.Empty<string>()
        };
    }

    private static string GetModeTemplate(string mode)
    {
        return mode switch
        {
            "Chat" => "System:\r\n{{SystemContext}}\r\n\r\nUser:\r\n{{UserRequest}}\r\n\r\nAssistant:\r\n",
            "System/User" => "System Message:\r\n{{SystemContext}}\r\n\r\nUser Prompt:\r\n{{UserRequest}}\r\n\r\nExpected Output:\r\n",
            "Checklist" => "Objective:\r\n\r\nChecklist Items:\r\n- {{ItemOne}}\r\n- {{ItemTwo}}\r\n\r\nAcceptance Criteria:\r\n",
            "Matrix" => "Context:\r\n\r\nRows:\r\n{{Rows}}\r\n\r\nColumns:\r\n{{Columns}}\r\n\r\nExpected Matrix Output:\r\n",
            "Protocol" => "Purpose:\r\n\r\nScope:\r\n\r\nProcedure:\r\n\r\nAcceptance Criteria:\r\n\r\nDeviations:\r\n",
            _ => "Purpose:\r\n\r\nInstructions:\r\n\r\nInputs:\r\n{{Input}}\r\n\r\nExpected Output:\r\n"
        };
    }

    private static IEnumerable<string> SplitLabels(string labels)
    {
        return labels
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(label => !string.IsNullOrWhiteSpace(label));
    }

    private static int CountOccurrences(string source, string value)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
        {
            return 0;
        }

        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static void ShowError(Exception ex)
    {
        MessageBox.Show(ex.Message, "Prompt Manager", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}







