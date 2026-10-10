using System.Text.RegularExpressions;

namespace PromptManager.Services;

public static class VariableRenderer
{
    private static readonly Regex Pattern = new(@"\{\{\s*([A-Za-z_][A-Za-z0-9_.-]*)\s*\}\}", RegexOptions.Compiled);

    public static IReadOnlyList<string> Names(string body) => Pattern.Matches(body)
        .Select(match => match.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray();

    public static string Render(string body, IReadOnlyDictionary<string, string> values) => Pattern.Replace(body,
        match => values.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
}
