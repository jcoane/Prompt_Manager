namespace PromptManager.Models;

public sealed class FolderOption
{
    public const int UnfiledId = 0;

    public FolderOption(int id, string path)
    {
        Id = id;
        Path = path;
    }

    public int Id { get; }

    public string Path { get; }

    public int? FolderId => Id == UnfiledId ? null : Id;
}
