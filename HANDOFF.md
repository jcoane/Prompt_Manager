# Prompt Manager Handoff Document

Prepared: 2026-06-18
Application: Prompt Manager
Project type: Standalone Windows 11 desktop application
Primary stack: C#, WPF, .NET 8, SQLite

---

## 1. Executive Summary

Prompt Manager is a local/offline Windows 11 desktop application for managing reusable prompts. It provides a familiar three-pane interface inspired by Windows Explorer, spreadsheet registers, controlled document libraries, validation matrices, and document-control systems.

The application supports creating, organizing, editing, versioning, searching, copying, archiving, importing, exporting, backing up, restoring, and deleting prompts. It also supports safe folder deletion, where folders and subfolders are removed but contained prompts are preserved by moving them to Unfiled.

The latest updates add a reusable custom Type field with `Prompt` as the default value, structured Mode options, reusable Labels, variable extraction/insertion, review checks, duplicate warnings, version comparison, and required change notes for body text changes. The Notes field is now a larger rich-text editor with bold, italic, and font color controls while retaining plain-text notes for search and export readability.

The application is intentionally local-only. It does not include cloud accounts, login, hosted collaboration, telemetry, analytics, online AI execution, API deployment, encryption, or network connectivity.

---

## 2. Current Delivery State

Current state: Built and packaged.

Verified items:

| Item | Status |
| --- | --- |
| WPF application build | Passed |
| SQLite database creation | Passed |
| Prompt register UI | Implemented |
| Search and clear search | Implemented |
| Prompt detail view and edit | Implemented |
| Custom prompt Type values | Implemented |
| Structured Mode field and templates | Implemented |
| Reusable labels | Implemented |
| Variables panel and insertion | Implemented |
| Review checks and duplicate warnings | Implemented |
| Version comparison and required change notes | Implemented |
| Larger formatted Notes editor | Implemented |
| Prompt copy to clipboard | Implemented |
| Prompt version tracking | Implemented |
| Archive and restore prompt | Implemented |
| Permanent prompt delete | Implemented |
| Safe folder delete | Implemented |
| Import and export JSON | Implemented |
| Backup and restore SQLite DB | Implemented |
| Single-file installer EXE | Built |
| User manual | Written |
| Metadata/Notes smoke test | Passed |

Latest installer artifact:

```text
C:\Users\jcoan\OneDrive\Validation Library\Val II\Documents\New project 2\artifacts\installer\PromptManagerSetup.exe
```

Final installer details:

```text
Size: 137,707,490 bytes
SHA-256: 0DB01B7F859E189C962AFB188A3F02C90FEA6C2D6E1B081E0C5F3092C66FDCDB
```

Default install location:

```text
C:\Program Files (x86)\Prompt Manager
```

The installer lets the user choose a different installation directory.

---

## 3. Project Location

Workspace root:

```text
C:\Users\jcoan\OneDrive\Validation Library\Val II\Documents\New project 2
```

Solution file:

```text
PromptManager.sln
```

Primary application project:

```text
src\PromptManager\PromptManager.csproj
```

Installer project:

```text
installer\PromptManagerSetup\PromptManagerSetup.csproj
```

---

## 4. Key Files

| Path | Purpose |
| --- | --- |
| `src\PromptManager\MainWindow.xaml` | Main WPF UI layout and styling |
| `src\PromptManager\MainWindow.xaml.cs` | Main window event handling, Help menu actions, folder selection, and formatted Notes editor handling |
| `src\PromptManager\ViewModels\MainViewModel.cs` | Main application state, commands, filtering, and UI actions |
| `src\PromptManager\ViewModels\RelayCommand.cs` | WPF command helper |
| `src\PromptManager\ViewModels\ObservableObject.cs` | Property notification base class |
| `src\PromptManager\Data\PromptRepository.cs` | SQLite schema, CRUD, search, versioning, import/export, backup/restore |
| `src\PromptManager\Services\PromptFileService.cs` | File dialogs and file-based import/export/backup/restore workflow |
| `src\PromptManager\Models\PromptRow.cs` | Prompt register row model |
| `src\PromptManager\Models\FolderNode.cs` | Folder tree model |
| `src\PromptManager\Models\ExportDtos.cs` | JSON export/import DTOs |
| `src\PromptManager\Models\PromptStatus.cs` | Prompt lifecycle statuses |
| `src\PromptManager\Dialogs\TextInputDialog.xaml` | Text input dialog for folder creation |
| `installer\PromptManagerSetup\Program.cs` | Installer UI and install logic |
| `README.md` | Developer quick-start and feature summary |
| `USER_MANUAL.md` | End-user manual and data model guide, included in installer payload |
| `HANDOFF.md` | This handoff document |

---

## 5. Runtime Data Locations

Prompt Manager creates its local application data under:

```text
%LOCALAPPDATA%\PromptManager
```

Primary SQLite database:

```text
%LOCALAPPDATA%\PromptManager\prompt-manager.db
```

The database is created automatically on first launch if it does not already exist.

Important handling notes:

- The app is offline and file-based.
- Backup copies the entire SQLite database.
- Restore replaces the active SQLite database.
- Export/import uses JSON and is better for moving selected prompt content.
- Backup/restore is better for whole-library replacement.

---

## 6. Architecture Overview

The app uses a simple layered WPF architecture.

```text
WPF Views
  MainWindow.xaml
  TextInputDialog.xaml

View Models
  MainViewModel
  FilterItem
  RelayCommand
  ObservableObject

Services
  PromptFileService

Data Access
  PromptRepository

Models / DTOs
  PromptRow
  FolderNode
  PromptStatus
  ExportDtos

Local Persistence
  SQLite database
  SQLite FTS5 virtual table when available
```

Responsibilities:

| Layer | Responsibility |
| --- | --- |
| View | Layout, styling, bindings, user controls |
| ViewModel | Commands, selected state, refresh logic, validation messages |
| Service | File dialogs and user-facing file operations |
| Repository | Database schema, SQL operations, import/export packages, backup/restore |
| Models | Transfer and display structures |

---

## 7. UI Summary

The main UI is a single-window three-pane layout.

```text
Top Bar
  Search, Clear Search, New Folder, Delete Folder, New Prompt, Import, Export, Backup, Restore

Left Pane
  Quick filters and directory tree

Center Pane
  Spreadsheet-style prompt register

Right Pane
  Prompt properties, body text, metadata, and prompt actions
```

The color system uses pastel tones, with darker colors at the top level and lighter shades for lower-level panels and controls.

---

## 8. Implemented Features

### Prompt Management

- Create new prompts.
- Edit title, type, mode, labels, path, tags, status, formatted notes, and prompt body.
- Insert `{{VariableName}}` placeholders and review detected variables.
- Run local review checks for prompt quality and duplicate title/body matches.
- Compare body versions and require a change note before saving body changes.
- Save changes.
- Move prompts between folders by changing the Properties panel Path dropdown and saving.
- Create body-text version records when prompt text changes.
- Copy prompt body to clipboard.
- Track last-used date when copied.
- Mark prompts as favorite.
- Archive prompts and mark them retired.
- Restore archived prompts as draft.
- Permanently delete prompts with confirmation.

### Folder Management

- Create top-level folders.
- Create child folders under the selected folder.
- Select a folder to filter the register to that folder and descendants.
- Clear folder scope through the All Folders tree item.
- Delete selected folder and all subfolders with confirmation.

Folder delete behavior is intentionally conservative:

```text
Delete Folder removes folders, but does not delete prompts.
Prompts in deleted folders are moved to Unfiled by setting FolderId = NULL.
```

### Search and Register Views

- Search type, mode, labels, title, body text, tags, and notes.
- Uses SQLite FTS5 when available.
- Falls back to local LIKE matching when FTS5 is unavailable.
- Clear Search command resets search text.
- Quick filters include All Prompts, Favorites, Recently Used, Drafts, Approved, and Retired.

### File Operations

- Export selected prompt or library context to JSON.
- Import JSON prompt packages.
- Backup local SQLite database to a selected file.
- Restore local SQLite database from selected backup file.

### Installation

- Single installer EXE created.
- Installer defaults to Program Files (x86).
- Installer provides folder picker for custom install location.
- Installer packages the self-contained app payload.
- Installer payload includes `USER_MANUAL.md` and `Assets\\ProtoCall_logo.png` for Help menu access and the About dialog.

---

## 9. Data Model Summary

### Tables

| Table | Purpose |
| --- | --- |
| `Folders` | Folder hierarchy |
| `Prompts` | Current prompt records and metadata |
| `PromptTypes` | Reusable custom Type values |
| `PromptLabels` | Reusable label values |
| `PromptVersions` | Version history for prompt body text |
| `PromptSearch` | FTS5 virtual table, when available |

### Folders

Key fields:

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | INTEGER | Primary key |
| `ParentId` | INTEGER nullable | Parent folder ID |
| `Name` | TEXT | Folder name |
| `CreatedAt` | TEXT | Database date/time text |

Relationship:

```text
ParentId -> Folders.Id ON DELETE CASCADE
```

### Prompts

Key fields:

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | INTEGER | Primary key |
| `FolderId` | INTEGER nullable | Folder ID; null means Unfiled |
| `Type` | TEXT | Custom type, defaults to Prompt |
| `Mode` | TEXT | Structured prompt mode |
| `Labels` | TEXT | Semicolon-delimited labels |
| `Title` | TEXT | Prompt title |
| `Body` | TEXT | Current prompt body |
| `Tags` | TEXT | Tag text |
| `Status` | TEXT | Draft, InReview, Approved, Retired |
| `Notes` | TEXT | Plain-text user notes used for search/readable exports |
| `FormattedNotes` | TEXT | RTF-formatted notes |
| `IsFavorite` | INTEGER | Boolean 0 or 1 |
| `IsArchived` | INTEGER | Boolean 0 or 1 |
| `CreatedAt` | TEXT | Database date/time text |
| `UpdatedAt` | TEXT | Database date/time text |
| `LastUsedAt` | TEXT nullable | Updated when copied |

Relationship:

```text
FolderId -> Folders.Id ON DELETE SET NULL
```

### PromptVersions

Key fields:

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | INTEGER | Primary key |
| `PromptId` | INTEGER | Parent prompt |
| `VersionNumber` | INTEGER | Version number |
| `Body` | TEXT | Version body |
| `ChangeNote` | TEXT | Version note |
| `CreatedAt` | TEXT | Database date/time text |

Relationship:

```text
PromptId -> Prompts.Id ON DELETE CASCADE
```

---

## 10. Important Code Paths

### Startup

1. `MainWindow.xaml.cs` creates and assigns `MainViewModel`.
2. `MainViewModel` creates `PromptRepository` and `PromptFileService`.
3. `PromptRepository` ensures the database folder exists.
4. `PromptRepository` creates schema if needed.
5. Starter sample data is created when the library is empty.
6. UI refreshes folder tree, counts, and prompt register.

### Search

Primary flow:

```text
MainWindow Search TextBox -> MainViewModel.SearchText -> RefreshPrompts -> PromptRepository.GetPrompts
```

The repository uses FTS5 if available. If FTS setup fails, search falls back to LIKE matching.

### Save Prompt

Primary flow:

```text
Save button -> SaveSelectedPrompt -> PromptRepository.UpdatePrompt
```

If prompt body text changes, the repository creates a new `PromptVersions` row.

### Delete Prompt

Primary flow:

```text
Delete button -> DeleteSelectedPrompt -> PromptRepository.DeletePrompt
```

This permanently deletes the prompt and cascades to prompt versions.

### Delete Folder

Primary flow:

```text
Delete Folder button -> DeleteSelectedFolder -> PromptRepository.DeleteFolder
```

The repository:

1. Finds the selected folder and descendant folder IDs.
2. Updates prompts in those folders to `FolderId = NULL`.
3. Deletes the selected folder.
4. Lets SQLite cascade delete subfolders.
5. Commits the change in a transaction.

---

## 11. Build and Run Instructions

.NET SDK currently used:

```text
C:\Program Files\dotnet\dotnet.exe
```

The shell used during development did not have `dotnet` on PATH, so use the full path when needed.

Build:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build PromptManager.sln
```

Run from source:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\src\PromptManager\PromptManager.csproj
```

Publish self-contained app payload:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' publish src\PromptManager\PromptManager.csproj -c Release -r win-x86 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=false -o artifacts\publish\PromptManager-win-x86
```

Create installer payload zip:

```powershell
Compress-Archive -Path (Join-Path 'artifacts\publish\PromptManager-win-x86' '*') -DestinationPath 'artifacts\installer\payload.zip' -CompressionLevel Optimal -Force
```

Publish installer wrapper:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' publish installer\PromptManagerSetup\PromptManagerSetup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishReadyToRun=false -o artifacts\installer\setup-publish
```

Copy final installer:

```powershell
Copy-Item -LiteralPath 'artifacts\installer\setup-publish\PromptManagerSetup.exe' -Destination 'artifacts\installer\PromptManagerSetup.exe' -Force
```

---

## 12. Verification Notes

Last known verification results:

| Verification | Result |
| --- | --- |
| Solution build | Passed, 0 warnings, 0 errors |
| App launch | Passed |
| Main controls visible | Passed |
| Search operation | Passed |
| Clear Search operation | Passed |
| Prompt view/select operation | Passed |
| Prompt delete operation | Passed |
| Folder delete operation | Passed |
| Folder delete prompt preservation | Passed |
| Installer rebuild | Passed |

Folder delete smoke-test result:

```text
FolderRowsAfter = 0
PromptFolderAfter = NULL
```

Temporary smoke-test records were removed from the local database after verification.

---

## 13. Packaging Notes

The installer is implemented as a custom WinForms setup wrapper in:

```text
installer\PromptManagerSetup\Program.cs
```

The installer embeds or carries:

```text
artifacts\installer\payload.zip
```

The app payload is produced at:

```text
artifacts\publish\PromptManager-win-x86
```

The installer output is:

```text
artifacts\installer\PromptManagerSetup.exe
```

When rebuilding the installer, always publish the app payload first, update `payload.zip`, then publish the setup project.

---

## 14. Operational Guidance

### For users

Use the installed Start Menu or desktop shortcut if created. If launching manually after install, run:

```text
C:\Program Files (x86)\Prompt Manager\PromptManager.exe
```

### For maintainers

Before making risky changes:

1. Back up `%LOCALAPPDATA%\PromptManager\prompt-manager.db`.
2. Run the app against a test database if possible.
3. Rebuild and launch the app.
4. Exercise search, prompt selection, save, copy, archive, restore, prompt delete, and folder delete.
5. Rebuild the installer after source changes.

### Delete behavior

Prompt delete is destructive.

Folder delete is intentionally not destructive to prompt records. It deletes the directory branch and moves contained prompts to Unfiled.

---

## 15. Known Constraints and Non-Goals

The following were intentionally excluded:

- Cloud sync
- SaaS accounts
- Login or identity management
- Hosted collaboration
- Online AI model execution
- API hosting
- Analytics or telemetry
- Encryption
- Network connectivity

Known technical constraints:

- No automated test project exists yet.
- Installer is custom code, not MSI/WiX/MSIX.
- Search depends on SQLite FTS5 availability, with LIKE fallback.
- Database restore replaces the active database file.
- Prompt delete permanently removes the prompt and its versions.

---

## 16. Suggested Next Improvements

Recommended future work:

1. Add automated repository tests using a temporary SQLite database.
2. Add a visible Unfiled filter or virtual folder in the directory tree.
3. Add rename folder functionality.
4. Add move prompt to folder functionality.
5. Add duplicate prompt command.
6. Add version history viewer and restore-version command.
7. Add CSV export for register-style review.
8. Add configurable backup reminder, still local-only.
9. Add installer uninstall support if a formal installer technology is adopted.
10. Add release notes per packaged build.

---

## 17. Handoff Checklist

Before transferring ownership, confirm:

| Check | Complete |
| --- | --- |
| Source code is present | Yes |
| README is present | Yes |
| User manual is present | Yes |
| Handoff document is present | Yes |
| App builds from solution | Yes |
| Installer artifact exists | Yes |
| Local database location documented | Yes |
| Offline-only scope documented | Yes |
| Delete behavior documented | Yes |
| Packaging commands documented | Yes |

---

## 18. Quick Reference

Build app:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build PromptManager.sln
```

Run app:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\src\PromptManager\PromptManager.csproj
```

Final installer:

```text
artifacts\installer\PromptManagerSetup.exe
```

User manual:

```text
USER_MANUAL.md
```

Local database:

```text
%LOCALAPPDATA%\PromptManager\prompt-manager.db
```


