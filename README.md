# Prompt Manager

Current version: **1.4.0**. Active project: `D:\dev\Prompt_Manager`.
Installer: `artifacts\installer\PromptManagerSetup.exe`. The self-contained installer includes the desktop runtime and offline manual.

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

Use an IDE supporting the installed .NET 10 SDK, or run:

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
- Selectable saved revisions, metadata and line differences, restore as a new Draft revision
- Full-text prompt search over type, mode, labels, title, body, tags, and notes where SQLite FTS5 is available
- Copy prompt text to clipboard with last-used tracking
- Local JSON import/export
- Local SQLite database backup/restore

## Version 1.4 Upgrades

- Embedded left-pointing flaming-arrow icons for the app, windows, Explorer, Setup, and installed shortcuts
- Compact File menu and folder commands, persistent columns/sort/pane settings, explicit unsaved indicator
- Full-height Notes/Prompt editors, reading size, and expandable editing windows
- Folder rename/move and transactional multi-prompt moves
- Offline variable filling, preview, and copy without changing the stored template
- Explicit selected/current-view/entire-library exports
- Optimistic edit revision checks, versioned archive/restore/folder relocation
- SQLite snapshot backups, staged validated restores, pre-upgrade backups, no automatic example reseeding
- Recovery table for detached historical entries, with complete original snapshots and a pre-recovery backup
- Installer refuses to close a running app and rolls back failed application-file updates

## Verification and Packaging

```powershell
dotnet build PromptManager.sln -c Release -p:NuGetAudit=false -m:1
dotnet run --project artifacts\smoke\UpgradeSafetySmoke -c Release --no-build
.\tools\Build-Installer.ps1
```

The regression harness uses temporary libraries and a test-only startup mode. The four original smoke projects remain in the solution. Verification notes and residual limitations are in `UPGRADE_NOTES.md`. No test pass constitutes formal validation or quality approval.




