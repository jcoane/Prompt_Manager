using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using PromptManager.Data;
using PromptManager.Models;

namespace PromptManager.Services;

public sealed class PromptFileService
{
    private readonly PromptRepository _repository;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public PromptFileService(PromptRepository repository)
    {
        _repository = repository;
    }

    public string? Export(IEnumerable<int>? promptIds, string scope)
    {
        var package = _repository.CreateScopedExport(promptIds);
        var defaultName = "prompt-" + scope.ToLowerInvariant().Replace(' ', '-') + "-export.json";

        var dialog = new SaveFileDialog
        {
            Title = "Export " + scope + " (" + package.Prompts.Count + " prompts)",
            Filter = "Prompt Manager export (*.json)|*.json|All files (*.*)|*.*",
            FileName = defaultName,
            AddExtension = true,
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(package, _jsonOptions);
        File.WriteAllText(dialog.FileName, json);
        return dialog.FileName;
    }

    public int? Import()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Prompts",
            Filter = "Prompt Manager export (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        var json = File.ReadAllText(dialog.FileName);
        var package = JsonSerializer.Deserialize<PromptExportPackage>(json, _jsonOptions);
        if (package is null || package.Format != "PromptManager.Export")
        {
            throw new InvalidOperationException("The selected file is not a valid Prompt Manager export.");
        }

        return _repository.ImportPackage(package);
    }

    public string? Backup()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Back Up Prompt Manager Database",
            Filter = "SQLite database backup (*.db)|*.db|All files (*.*)|*.*",
            FileName = "prompt-manager-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".db",
            AddExtension = true,
            DefaultExt = ".db"
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        _repository.BackupTo(dialog.FileName);
        return dialog.FileName;
    }

    public string? Restore()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Restore Prompt Manager Database",
            Filter = "SQLite database backup (*.db)|*.db|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        PromptRepository.ValidateBackupDatabase(dialog.FileName);

        var confirm = MessageBox.Show(
            "Restore this database backup? The current local prompt library will be replaced after a safety backup is created.",
            "Restore Prompt Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return null;
        }

        return _repository.RestoreFrom(dialog.FileName) ?? "";
    }
}


