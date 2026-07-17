using PromptManager.Data;
using PromptManager.Models;

var repository = new PromptRepository();
repository.Initialize();

var promptId = repository.CreatePrompt(null);
try
{
    var prompt = repository.GetPromptById(promptId) ?? throw new InvalidOperationException("Created prompt was not found.");
    prompt.Title = "Smoke Test Custom Type";
    prompt.Type = "Validation Matrix";
    prompt.Mode = "Matrix";
    prompt.Labels = "Training; Production";
    prompt.Body = "Smoke-test body for custom prompt metadata.";
    prompt.Notes = "Formatted note text";
    prompt.FormattedNotes = @"{\rtf1\ansi\b Formatted\b0  note text}";

    var saved = repository.SavePrompt(prompt, "Smoke test metadata update");
    var reloaded = repository.GetPromptById(saved.Id) ?? throw new InvalidOperationException("Saved prompt was not found.");

    Require(reloaded.Type == "Validation Matrix", "Custom Type did not persist.");
    Require(reloaded.Mode == "Matrix", "Prompt Mode did not persist.");
    Require(reloaded.Labels.Contains("Production", StringComparison.Ordinal), "Prompt Labels did not persist.");
    Require(reloaded.FormattedNotes.Contains(@"\b Formatted", StringComparison.Ordinal), "Formatted notes did not persist.");
    Require(repository.GetPromptTypes().Contains("Validation Matrix"), "Custom Type was not added to selectable Type list.");
    Require(repository.GetPromptLabels().Contains("Production"), "Label catalog did not include saved labels.");
    Require(repository.GetPrompts(PromptFilterKind.All, null, "Validation Matrix").Any(row => row.Id == reloaded.Id), "Custom Type was not searchable.");
    Require(repository.GetPrompts(PromptFilterKind.All, null, "Production").Any(row => row.Id == reloaded.Id), "Labels were not searchable.");
    Require(repository.GetPromptVersions(reloaded.Id).Any(version => version.ChangeNote == "Smoke test metadata update"), "Change note was not saved with version history.");

    Console.WriteLine("PASS custom type, mode, labels, search, change note, and formatted notes persistence.");
}
finally
{
    repository.DeletePrompt(promptId);
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
