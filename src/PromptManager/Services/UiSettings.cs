using System.IO;
using System.Text.Json;

namespace PromptManager.Services;

public sealed class UiSettings
{
    public double NavigationWidth { get; set; } = 220;
    public double EditorWidth { get; set; } = 500;
    public int ReadingSize { get; set; } = 16;
    public List<ColumnSetting> Columns { get; set; } = new();
    public List<SortSetting> Sort { get; set; } = new();

    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PromptManager", "ui-settings.json");
    public static UiSettings Load(string path)
    {
        try { return JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(path)) ?? new(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
}

public sealed record ColumnSetting(string Name, bool Visible, double Width, int Order);
public sealed record SortSetting(string Property, bool Descending);
