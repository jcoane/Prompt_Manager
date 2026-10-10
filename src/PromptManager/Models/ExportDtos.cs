namespace PromptManager.Models;

public sealed class PromptExportPackage
{
    public string Format { get; set; } = "PromptManager.Export";

    public string FormatVersion { get; set; } = "1.4";

    public DateTime ExportedAt { get; set; } = DateTime.Now;

    public List<FolderExportDto> Folders { get; set; } = new();

    public List<PromptExportDto> Prompts { get; set; } = new();
    public List<RecoveryHistoryDto> RecoveryHistory { get; set; } = new();
}

public sealed class RecoveryHistoryDto
{
    public long OriginalVersionId { get; set; }
    public long OriginalPromptId { get; set; }
    public int VersionNumber { get; set; }
    public string SnapshotJson { get; set; } = "";
    public DateTime RecoveredAt { get; set; }
    public string Reason { get; set; } = "";
}

public sealed class FolderExportDto
{
    public int Id { get; set; }

    public int? ParentId { get; set; }

    public string Name { get; set; } = "";
}

public sealed class PromptExportDto
{
    public int Id { get; set; }

    public int? FolderId { get; set; }

    public string Title { get; set; } = "";

    public string Type { get; set; } = "Prompt";

    public string Mode { get; set; } = "Text";

    public string Labels { get; set; } = "";

    public string Body { get; set; } = "";

    public PromptStatus Status { get; set; }

    public string Tags { get; set; } = "";

    public string Notes { get; set; } = "";

    public string FormattedNotes { get; set; } = "";

    public bool IsFavorite { get; set; }

    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public List<PromptVersionExportDto> Versions { get; set; } = new();
}

public sealed class PromptVersionExportDto
{
    public int VersionNumber { get; set; }

    public int? FolderId { get; set; }
    public string? FolderPath { get; set; }
    public bool? IsArchived { get; set; }
    public string DisplayName => $"v{VersionNumber} | {CreatedAt:g} | {ChangeNote}";

    public string? Title { get; set; }

    public string? Type { get; set; }

    public string? Mode { get; set; }

    public string? Labels { get; set; }

    public string Body { get; set; } = "";

    public string? Status { get; set; }

    public string? Tags { get; set; }

    public string? Notes { get; set; }

    public string? FormattedNotes { get; set; }

    public string ChangeNote { get; set; } = "";

    public DateTime CreatedAt { get; set; }
}


