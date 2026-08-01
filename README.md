# Prompt Manager

Prompt Manager is a standalone Windows 11 desktop application for managing reusable prompts locally.

## Stack

- C# / WPF
- .NET 10 Windows desktop
- SQLite local database
- SQLite FTS5 search when available, with a local `LIKE` fallback
- Local JSON import/export
- Local database backup/restore

## Privacy / Connectivity

This app is intentionally offline-only. It has no login, no cloud sync, no telemetry, no API hosting, and no AI model execution.

## Build

Open `PromptManager.sln` in Visual Studio 2022 or run:

```powershell
dotnet restore
dotnet build
dotnet run --project .\src\PromptManager\PromptManager.csproj
```

The local database is created under:

```text
%LOCALAPPDATA%\PromptManager\prompt-manager.db
```

## Current Features

- Three-pane Windows-style desktop UI
- Help menu with User Manual and About dialog
- Prompt registers with spreadsheet-like sorting and filtering
- Folder tree and quick filters with folder deletion and prompt moves
- Editable custom Type field with reusable saved Type values
- Structured Mode field with Text, Chat, System/User, Checklist, Matrix, and Protocol options
- Labels with quick-add catalog for Production, Approved, Training, Deprecated, and custom labels
- Larger formatted Notes editor with bold, italic, and font color controls
- Variables panel and `{{VariableName}}` insertion
- Review checks with duplicate title/body warnings and prompt quality checks
- Draft, review, approved, and retired statuses
- Favorites, recently used, archive, restore, permanent prompt delete, and safe folder delete
- Prompt path dropdown for moving prompts between folders
- Prompt version history with required change notes for controlled record changes
- Version comparison summary
- Full-text prompt search over type, mode, labels, title, body, tags, and notes where SQLite FTS5 is available
- Copy prompt text to clipboard with last-used tracking
- Local JSON import/export
- Local SQLite database backup/restore




