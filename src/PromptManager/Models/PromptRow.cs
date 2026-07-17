using PromptManager.ViewModels;

namespace PromptManager.Models;

public sealed class PromptRow : ObservableObject
{
    private string _title = "";
    private string _type = "Prompt";
    private string _mode = "Text";
    private string _labels = "";
    private PromptStatus _status = PromptStatus.Draft;
    private string _tags = "";
    private string _notes = "";
    private string _formattedNotes = "";
    private string _body = "";
    private bool _isFavorite;
    private bool _isArchived;
    private int? _folderId;
    private string _folderPath = "";

    public int Id { get; init; }

    public int? FolderId
    {
        get => _folderId;
        set => SetProperty(ref _folderId, value);
    }

    public string Type
    {
        get => _type;
        set => SetProperty(ref _type, string.IsNullOrWhiteSpace(value) ? "Prompt" : value.Trim());
    }

    public string Mode
    {
        get => _mode;
        set => SetProperty(ref _mode, string.IsNullOrWhiteSpace(value) ? "Text" : value.Trim());
    }

    public string Labels
    {
        get => _labels;
        set => SetProperty(ref _labels, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public PromptStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string Tags
    {
        get => _tags;
        set => SetProperty(ref _tags, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string FormattedNotes
    {
        get => _formattedNotes;
        set => SetProperty(ref _formattedNotes, value);
    }

    public string Body
    {
        get => _body;
        set => SetProperty(ref _body, value);
    }

    public string FolderPath
    {
        get => _folderPath;
        set => SetProperty(ref _folderPath, value);
    }

    public int Version { get; init; }

    public string VersionLabel => $"v{Version}";

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetProperty(ref _isFavorite, value);
    }

    public bool IsArchived
    {
        get => _isArchived;
        set => SetProperty(ref _isArchived, value);
    }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    public DateTime? LastUsedAt { get; init; }
}
