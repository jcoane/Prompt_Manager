using System.Text;
using PromptManager.Models;

namespace PromptManager.Services;

public static class VersionComparison
{
    public static string Compare(PromptVersionExportDto saved, PromptRow current, string? currentPath = null)
    {
        var result = new StringBuilder();
        Field("Title", saved.Title, current.Title);
        Field("Type", saved.Type, current.Type);
        Field("Mode", saved.Mode, current.Mode);
        Field("Labels", saved.Labels, current.Labels);
        Field("Status", saved.Status, current.Status.ToString());
        Field("Path", string.IsNullOrEmpty(saved.FolderPath) ? null : saved.FolderPath, currentPath ?? current.FolderPath);
        Field("Tags", saved.Tags, current.Tags);
        Field("Notes", saved.Notes, current.Notes);
        if (saved.FormattedNotes != null && saved.FormattedNotes != current.FormattedNotes)
            result.AppendLine("Notes formatting: changed");
        if (saved.IsArchived.HasValue && saved.IsArchived != current.IsArchived)
            result.AppendLine($"Archived: {saved.IsArchived} -> {current.IsArchived}");
        if (saved.Body == current.Body) result.AppendLine("Prompt text: unchanged");
        else result.AppendLine("Prompt text:").Append(LineDiff(saved.Body, current.Body));
        return result.Length == 0 ? "No differences." : result.ToString();

        void Field(string name, string? oldValue, string newValue)
        {
            if (oldValue == null) result.AppendLine(name + ": not recorded in this revision");
            else if (oldValue != newValue) result.AppendLine($"{name}: {oldValue} -> {newValue}");
        }
    }

    public static string LineDiff(string before, string after)
    {
        var a = before.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var b = after.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        // Bound memory for very large prompts; both complete texts remain available in the editor.
        if ((long)(a.Length + 1) * (b.Length + 1) > 1_000_000)
            return "Comparison is too large for an inline diff. Saved and current text are available separately.";
        var lengths = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
            for (var j = b.Length - 1; j >= 0; j--)
                lengths[i, j] = a[i] == b[j] ? 1 + lengths[i + 1, j + 1] : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
        var output = new StringBuilder();
        var x = 0; var y = 0;
        while (x < a.Length || y < b.Length)
        {
            if (x < a.Length && y < b.Length && a[x] == b[y]) { output.AppendLine("  " + a[x++]); y++; }
            else if (y < b.Length && (x == a.Length || lengths[x, y + 1] >= lengths[x + 1, y])) output.AppendLine("+ " + b[y++]);
            else output.AppendLine("- " + a[x++]);
        }
        return output.ToString();
    }
}
