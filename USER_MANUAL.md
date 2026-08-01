# Prompt Manager User Manual

**Application:** Prompt Manager  
**Document Type:** User Manual and Technical Reference  
**Version:** 1.3  
**Platform:** Windows 11 desktop  
**Storage Model:** Local/offline SQLite database  
**Prepared For:** Prompt library users, validation document authors, controlled document owners, and local application administrators

---

## Document Control

| Field | Value |
| --- | --- |
| Product Name | Prompt Manager |
| Application Type | Standalone Windows desktop application |
| Primary Audience | End users and local administrators |
| Data Location | `%LOCALAPPDATA%\PromptManager\prompt-manager.db` |
| Connectivity | Offline/local only |
| Authentication | None |
| Telemetry | None |
| Encryption | Not implemented |
| AI Execution | Not implemented |

### Revision History

| Version | Date | Description |
| --- | --- | --- |
| 1.0 | 2026-05-07 | Initial user manual for Prompt Manager |
| 1.1 | 2026-06-18 | Added custom prompt Type values and formatted Notes support |
| 1.2 | 2026-06-18 | Added modes, labels, variables, review checks, duplicate warnings, version comparison, and required change notes |
| 1.3 | 2026-08-01 | Added tabbed properties panel, .NET 10 target, safer restore, fuller version metadata, safer installer manifest handling, and isolated smoke checks |

---

## 1. Introduction

Prompt Manager is a standalone Windows 11 desktop application for creating, organizing, editing, searching, copying, archiving, importing, exporting, backing up, and restoring reusable prompts.

The application is intended for users who work with structured documentation, validation libraries, controlled document registers, protocol templates, CSV documentation, qualification matrices, and reusable writing assets. Its layout intentionally resembles familiar desktop tools such as Windows Explorer, spreadsheet registers, and controlled document libraries.

Prompt Manager is local-first and offline-only. It does not provide cloud accounts, hosted collaboration, API access, telemetry, online AI model execution, or network synchronization.

### 1.1 Primary Use Cases

Use Prompt Manager to:

- Maintain a reusable prompt library.
- Organize prompts into folder structures.
- Classify prompts using reusable custom Type values.
- Assign structured modes and labels.
- Track reusable `{{Variables}}` in prompt text.
- Mark prompts as draft, in review, approved, or retired.
- Search prompt type, mode, labels, titles, tags, notes, and prompt text.
- Copy approved or reusable prompt text to the Windows clipboard.
- Track prompt versions when controlled prompt fields change.
- Archive retired prompts without permanently deleting them.
- Import and export prompt libraries as JSON files.
- Back up and restore the local SQLite database.

### 1.2 Intended Users

Prompt Manager is suitable for:

- Validation engineers
- CSV specialists
- Quality system authors
- Technical writers
- Business development proposal writers
- Local prompt library owners
- Desktop users who prefer file-based control over hosted services

---

## 2. Safety, Privacy, and Offline Behavior

Prompt Manager is designed as a local desktop tool.

### 2.1 What the Application Does

- Stores prompt data in a local SQLite database.
- Opens local Windows file dialogs for import, export, backup, and restore.
- Copies selected prompt text to the Windows clipboard.
- Saves prompt version records when controlled prompt fields change.
- Creates a local full-text search index when SQLite FTS5 is available.

### 2.2 What the Application Does Not Do

Prompt Manager does not:

- Require a login.
- Create user accounts.
- Connect to a cloud service.
- Execute prompts against AI models.
- Send data to external APIs.
- Host a web service.
- Capture analytics telemetry.
- Encrypt the database.
- Provide multi-user conflict control.
- Provide electronic signature workflow.
- Replace a validated document management system.

### 2.3 Important Data Handling Notes

The database is local to the Windows user profile. Anyone with access to the Windows account and the database file may be able to read the prompt data. Use Windows account controls, normal workstation security, and local backup discipline as appropriate for your environment.

Prompt Manager records versions of controlled prompt fields when saved, but it is not a complete audit trail system. It does not currently capture user identity, electronic signatures, reason-for-change prompts, or approval signatures.

---

## 3. Getting Started

### 3.1 System Requirements

| Requirement | Value |
| --- | --- |
| Operating System | Windows 11 recommended |
| Runtime | Installed package is self-contained; .NET 10 Windows Desktop Runtime for development builds |
| Build SDK | .NET SDK 10.0 if building from source |
| Database | SQLite local database |
| Disk Access | User write access to `%LOCALAPPDATA%` |

### 3.2 Launching the Application

If running from the development build, launch:

```text
src\PromptManager\bin\Debug\net10.0-windows\PromptManager.exe
```

If running from Visual Studio, open:

```text
PromptManager.sln
```

Then start the `PromptManager` project.

### 3.3 First Launch

On first launch, Prompt Manager creates the local application data folder and database:

```text
%LOCALAPPDATA%\PromptManager\prompt-manager.db
```

If the database is empty, the app creates starter folders and sample prompts:

```text
Validation
  CSV
  Equipment Qualification
Software / CSV
Business Dev
```

Starter prompts include:

- CSV Protocol Builder
- Part 11 Test Matrix
- IQ/OQ Generator
- Change Control Impact Review

---

## 4. Main Window Overview

Prompt Manager uses a single-window, three-pane layout.

```text
+--------------------------------------------------------------------------------+
| Top Bar: Search | New Folder | Delete Folder | New Prompt | Import | Export | Backup | Restore |
+--------------------------------------------------------------------------------+
| Left Navigation       | Center Prompt Register                  | Properties     |
|                       |                                         | Panel          |
| Views                 | Type | Mode | Name | Status | Labels ...| Selected item  |
| Directory Tree        |                                         | edit controls  |
|                       | Spreadsheet-style prompt list           | document info  |
+--------------------------------------------------------------------------------+
| Status Bar: local database path                                                |
+--------------------------------------------------------------------------------+
```

### 4.1 Help Menu

The menu row includes a `Help` menu.

| Menu Item | Purpose |
| --- | --- |
| User Manual | Opens the installed `USER_MANUAL.md` file |
| About | Shows the ProtoCall logo, author line, and a brief software description |

The About dialog displays `Autored By John-Patrick Coane` and describes Prompt Manager as a standalone offline prompt library manager.

### 4.2 Top Bar

The top bar contains search and library-level actions.

| Control | Purpose |
| --- | --- |
| Search prompts | Searches prompt type, title, tags, notes, and body text |
| New Folder | Creates a folder under the selected folder |
| Delete Folder | Deletes the selected folder and subfolders, moving contained prompts to Unfiled |
| New Prompt | Creates a draft prompt in the selected folder |
| Import | Imports prompts and folders from a JSON export file |
| Export | Exports the selected prompt, or the library context, to JSON |
| Backup | Copies the local SQLite database to a selected backup file |
| Restore | Replaces the local database from a selected backup file |

### 4.3 Left Navigation Pane

The left pane provides register filters and folder navigation.

#### Views

| View | Meaning |
| --- | --- |
| All Prompts | Active, non-archived prompts |
| Favorites | Prompts marked as favorites |
| Recently Used | Prompts copied to the clipboard at least once |
| Drafts | Active prompts with status `Draft` |
| Approved | Active prompts with status `Approved` |
| Retired | Prompts with status `Retired` or archived prompts |

Each view displays a count to help users understand the current library state.

#### Directory Tree

The directory tree organizes prompts into folders. Selecting a folder filters the center prompt register to that folder and its child folders.

### 4.4 Center Prompt Register

The center pane is a spreadsheet-style prompt list.

| Column | Description |
| --- | --- |
| Type | User-defined item type; defaults to `Prompt` |
| Mode | Structured prompt mode such as Text, Chat, Checklist, Matrix, or Protocol |
| Name | Prompt title |
| Status | Current prompt lifecycle status |
| Labels | User-managed use labels |
| Tags | User-entered classification tags |
| Path | Folder path shown in the register |
| Version | Current body-text version label |
| Favorite | Favorite flag |
| Modified | Last modified date/time |

Double-clicking a prompt copies its prompt text to the Windows clipboard.

### 4.5 Properties Panel

The right pane displays editable properties for the selected prompt in tabs: Details, Notes, Prompt, Review, and Versions.

| Field or Control | Purpose |
| --- | --- |
| Selected Item | Shows selected prompt title and folder path |
| Title | Editable prompt name |
| Type | Editable dropdown of custom Type values; new saved values become selectable |
| Mode | Structured prompt mode dropdown with template insertion |
| Path | Editable folder path dropdown; choose `Unfiled` or a folder path and click `Save` to move the prompt |
| Tags | Editable tag text |
| Labels | Editable labels plus quick-add label dropdown |
| Status | Editable lifecycle status |
| Notes | Larger formatted notes area with bold, italic, and font color controls |
| Prompt Text | Editable reusable prompt body |
| Variables | Lists `{{VariableName}}` placeholders detected in Prompt Text |
| Review Checks | Shows quality checks and duplicate title/body warnings |
| Version Control | Shows version comparison and captures required change notes for controlled record changes |
| Save | Saves changes |
| Copy | Copies prompt text to clipboard |
| Favorite | Toggles favorite flag |
| Archive | Archives the prompt and marks it retired |
| Restore | Restores an archived prompt as draft |
| Refresh | Reloads data from the local database |
| Document Control | Shows version, last modified, and last used metadata |

### 4.6 Status Bar

The bottom status bar displays the current local database path.

---

## 5. Common Workflows

### 5.1 Create a Folder

1. Select a parent folder in the Directory Tree, or leave no folder selected for a top-level folder.
2. Click `New Folder`.
3. Enter the folder name.
4. Click `OK`.

The new folder appears in the Directory Tree.

### 5.2 Delete a Folder

Use Delete Folder when a directory structure is no longer needed.

1. Select the folder in the Directory Tree.
2. Click `Delete Folder` in the top bar.
3. Confirm the warning dialog.

Delete Folder removes the selected folder and all subfolders. Prompts contained in those folders are not deleted; they are moved to Unfiled by clearing their folder assignment.

### 5.3 Create a Prompt

1. Select the destination folder in the Directory Tree.
2. Click `New Prompt`.
3. A new prompt named `Untitled Prompt` is created.
4. Edit the Title, Type, Mode, Path, Tags, Labels, Status, Notes, and Prompt Text in the Properties panel.
5. Confirm or change the Path dropdown if the prompt should be stored somewhere else.
6. Click `Save`.

New prompts start with status `Draft` and a starter body:

```text
Purpose:

Instructions:

Inputs:

Expected Output:
```

To create a custom Type, select a prompt, type the new value into the Type field, and click `Save`. The value is stored locally and becomes available in the Type dropdown for future prompt records. If no custom Type is selected, the default value is `Prompt`.

Use Mode to select a prompt structure. Available modes are `Text`, `Chat`, `System/User`, `Checklist`, `Matrix`, and `Protocol`. Click `Template` to append a starter structure for the selected mode.

### 5.4 Edit a Prompt

1. Select a prompt in the center register.
2. Edit fields in the Properties panel.
3. Click `Save`.

If the prompt text changes, Prompt Manager requires a Change Note before saving and creates a new version record using that note. Metadata changes such as title, type, mode, labels, tags, status, notes, favorite flag, and archive flag update the prompt record but do not create a new prompt body version by themselves.

### 5.4.1 Use Labels

Labels help identify intended use or lifecycle context.

1. Select a prompt.
2. Enter labels directly in the Labels field, separated by semicolons.
3. Or choose a label from the quick-add dropdown and click `Add`.
4. Click `Save`.

Common labels include:

| Label | Suggested Use |
| --- | --- |
| Production | Routine use |
| Approved | Reviewed and accepted |
| Training | Training or example use |
| Deprecated | Avoid for new work |

### 5.4.2 Use Variables

Variables are prompt placeholders using double braces.

```text
{{SystemName}}
{{UserRequest}}
{{Rows}}
```

To insert a variable:

1. Place the cursor in Prompt Text.
2. Click `Insert Variable`.
3. Enter a variable name.
4. The variable is inserted into the prompt as `{{VariableName}}`.

Detected variables appear in the Variables panel.

### 5.4.3 Review Checks and Duplicate Warnings

The Review Checks panel provides local quality checks. It may warn when:

- Title is still generic.
- Tags or labels are missing.
- Prompt text is very short.
- Variable braces are unbalanced.
- TODO or TBD text remains.
- The selected Mode is missing expected sections.
- An approved prompt is missing an Approved or Production label.
- Another active prompt has the same title or exact body text.

These checks are advisory and do not send data outside the PC.

### 5.5 Move a Prompt to Another Folder

1. Select the prompt in the center register.
2. In the Properties panel, open the `Path` dropdown.
3. Choose `Unfiled` or the target folder path.
4. Click `Save`.

Prompt Manager updates the prompt's folder assignment, refreshes the directory tree and register, and keeps the moved prompt visible in its destination folder when practical.

### 5.6 Change Prompt Status

1. Select a prompt.
2. Open the Status dropdown.
3. Choose one of:

| Status | Typical Meaning |
| --- | --- |
| Draft | Work in progress |
| InReview | Under review before use |
| Approved | Accepted for routine use |
| Retired | No longer recommended for active use |

4. Click `Save`.

### 5.7 Search for a Prompt

1. Click the search box in the top bar.
2. Type one or more search terms.
3. Review matching prompts in the center register.

Search covers:

- Type
- Mode
- Labels
- Title
- Prompt text
- Tags
- Notes

When SQLite FTS5 is available, Prompt Manager uses full-text search. If FTS5 is unavailable, the app falls back to local `LIKE` matching.

Search terms are converted into prefix-style full-text terms. For example:

```text
CSV protocol
```

is searched as:

```text
CSV* AND protocol*
```

### 5.8 Use a Prompt

1. Select a prompt in the register.
2. Click `Copy`, or double-click the prompt row.
3. Paste the prompt text into the target application.

When a prompt is copied, Prompt Manager updates `LastUsedAt`. The prompt then appears in the `Recently Used` view.

### 5.9 Mark a Prompt as Favorite

1. Select a prompt.
2. Click `Favorite`.

The favorite flag is toggled. Favorite prompts appear in the `Favorites` view.

### 5.10 Archive a Prompt

1. Select the prompt to retire.
2. Click `Archive`.
3. Confirm the archive action.

The prompt is marked as archived and its status is set to `Retired`. Archived prompts are visible in the `Retired` view.

### 5.11 Restore an Archived Prompt

1. Open the `Retired` view.
2. Select the archived prompt.
3. Click `Restore`.

The prompt is restored as active and its status is set to `Draft`.


### 5.12 Delete a Prompt

Use Delete only when a prompt should be permanently removed from the local library.

1. Select the prompt in the center register.
2. Click `Delete` in the Properties panel.
3. Confirm the warning dialog.

Delete removes the prompt record and its version history. This is different from Archive. Use Archive when a prompt should be retained for reference but removed from active use.

### 5.13 Refresh the Register

Click `Refresh` to reload folders, counts, and prompts from the local database.

---

## 6. Import, Export, Backup, and Restore

Prompt Manager uses local file operations only.

### 6.1 Export Prompts

1. Select a prompt if you want to export a specific prompt.
2. Click `Export`.
3. Choose the export file location.
4. Save the JSON file.

Export files use this extension by convention:

```text
.json
```

Default file names:

| Export Context | Default File Name |
| --- | --- |
| Selected prompt | `prompt-export.json` |
| Library export | `prompt-library-export.json` |

### 6.2 Import Prompts

1. Click `Import`.
2. Select a Prompt Manager JSON export file.
3. Confirm the import.

The import process recreates folder structures where needed. Existing folders with the same name under the same parent are reused. Imported prompts are inserted as new prompt records.

### 6.3 Back Up the Database

1. Click `Backup`.
2. Choose a destination.
3. Save the `.db` backup file.

Backup copies the entire SQLite database file.

Default backup file name format:

```text
prompt-manager-backup-yyyyMMdd-HHmm.db
```

Example:

```text
prompt-manager-backup-20260507-1815.db
```

### 6.4 Restore the Database

1. Click `Restore`.
2. Select a previously backed-up `.db` file.
3. Confirm the restore warning.

Restore replaces the current local prompt database with the selected backup file. Use this carefully. Any prompts created after the backup was taken may be lost unless they were exported or backed up separately.

---

## 7. Recommended Library Practices

### 7.1 Folder Organization

Recommended folder examples:

```text
Validation
  CSV
  Equipment Qualification
  Process Validation
Software / CSV
  GxP Assessments
  Risk Assessments
Business Dev
  Proposal Boilerplate
  Client Discovery
Quality Systems
  Deviation
  CAPA
  Change Control
```

### 7.2 Tagging

Use consistent tag patterns. Tags are stored as plain text, so consistency improves search and filtering.

Examples:

```text
CSV; protocol; validation
Part 11; test matrix; compliance
IQ; OQ; equipment
change control; impact; software
```

### 7.3 Status Governance

Suggested lifecycle:

```text
Draft -> InReview -> Approved -> Retired
```

Suggested usage:

| Status | Suggested Rule |
| --- | --- |
| Draft | Prompt is being written or tested |
| InReview | Prompt is ready for review but not routine use |
| Approved | Prompt is suitable for routine reuse |
| Retired | Prompt should not be used for new work |

### 7.4 Version Discipline

Prompt Manager creates a version record when controlled prompt fields change and the prompt is saved. For controlled prompt libraries:

- Keep titles stable and descriptive.
- Use Type, Tags, and Notes to describe intended usage and make records easier to retrieve.
- Use formatted Notes for review comments, usage cautions, and local document-control context.
- Retire outdated prompts instead of overwriting them for unrelated purposes.
- Export the library before major reorganizations.
- Back up the database before bulk imports.

---

## 8. File Structures

This section documents the project files, runtime files, export files, and database structures.

### 8.1 Source Project Structure

```text
PromptManager.sln
README.md
USER_MANUAL.md
.gitignore
src
  PromptManager
    PromptManager.csproj
    app.manifest
    App.xaml
    App.xaml.cs
    AboutWindow.xaml
    AboutWindow.xaml.cs
    MainWindow.xaml
    MainWindow.xaml.cs
    Assets
      ProtoCall_logo.png
    Data
      PromptRepository.cs
    Dialogs
      TextInputDialog.xaml
      TextInputDialog.xaml.cs
    Models
      ExportDtos.cs
      FolderNode.cs
      PromptFilterKind.cs
      PromptRow.cs
      PromptStatus.cs
    Services
      PromptFileService.cs
    ViewModels
      FilterItem.cs
      MainViewModel.cs
      ObservableObject.cs
      RelayCommand.cs
installer
  PromptManagerSetup
    PromptManagerSetup.csproj
    Program.cs
```

### 8.2 Build Output Structure

After a debug build:

```text
src
  PromptManager
    bin
      Debug
        net10.0-windows
          PromptManager.exe
          PromptManager.dll
          PromptManager.deps.json
          PromptManager.runtimeconfig.json
          Microsoft.Data.Sqlite.dll
          SQLitePCLRaw.batteries_v2.dll
          SQLitePCLRaw.core.dll
          SQLitePCLRaw.provider.e_sqlite3.dll
          runtimes
            win-x64
              native
                e_sqlite3.dll
```

### 8.3 Runtime Data Structure

Prompt Manager stores local data under:

```text
%LOCALAPPDATA%\PromptManager
```

Primary database:

```text
%LOCALAPPDATA%\PromptManager\prompt-manager.db
```

Possible SQLite companion files while the database is active:

```text
prompt-manager.db-wal
prompt-manager.db-shm
```

These files are normal SQLite write-ahead logging or shared-memory files if SQLite creates them.

### 8.4 Backup File Structure

A backup is a full copy of the SQLite database:

```text
prompt-manager-backup-yyyyMMdd-HHmm.db
```

Backup files contain all folders, prompts, prompt versions, and search index data available at the time of backup.

### 8.5 Export File Structure

Exports are JSON files using the `PromptManager.Export` format.

Top-level structure:

```json
{
  "Format": "PromptManager.Export",
  "FormatVersion": "1.3",
  "ExportedAt": "2026-05-07T18:15:00.0000000-04:00",
  "Folders": [],
  "Prompts": []
}
```

Folder entry:

```json
{
  "Id": 1,
  "ParentId": null,
  "Name": "Validation"
}
```

Prompt entry:

```json
{
  "Id": 1,
  "FolderId": 2,
  "Type": "Prompt",
  "Mode": "Text",
  "Labels": "Training; Approved",
  "Title": "CSV Protocol Builder",
  "Body": "Create a validation protocol outline...",
  "Status": "Draft",
  "Tags": "CSV; protocol; validation",
  "Notes": "",
  "FormattedNotes": "",
  "IsFavorite": false,
  "IsArchived": false,
  "CreatedAt": "2026-05-07T18:15:00.0000000-04:00",
  "UpdatedAt": "2026-05-07T18:15:00.0000000-04:00",
  "LastUsedAt": null,
  "Versions": []
}
```

Version entry:

```json
{
  "VersionNumber": 1,
  "FolderId": 2,
  "Title": "CSV Protocol Builder",
  "Type": "Prompt",
  "Mode": "Text",
  "Labels": "Training; Approved",
  "Body": "Create a validation protocol outline...",
  "Status": "Draft",
  "Tags": "CSV; protocol; validation",
  "Notes": "",
  "FormattedNotes": "",
  "ChangeNote": "Initial draft",
  "CreatedAt": "2026-05-07T18:15:00.0000000-04:00"
}
```

---

## 9. Data Types and Database Schema

Prompt Manager uses SQLite. SQLite uses flexible storage classes, but the application uses the logical data types described below.

### 9.1 Application Enumerations

#### PromptStatus

| Value | Description |
| --- | --- |
| Draft | Draft prompt |
| InReview | Prompt under review |
| Approved | Approved prompt |
| Retired | Retired prompt |

#### PromptFilterKind

| Value | Description |
| --- | --- |
| All | Active, non-archived prompts |
| Favorites | Favorite active prompts |
| RecentlyUsed | Active prompts with `LastUsedAt` populated |
| Drafts | Active prompts with status `Draft` |
| Approved | Active prompts with status `Approved` |
| Retired | Retired or archived prompts |

### 9.2 SQLite Table: Folders

| Column | SQLite Type | Nullable | Description |
| --- | --- | --- | --- |
| Id | INTEGER | No | Primary key, auto-increment |
| ParentId | INTEGER | Yes | Parent folder ID; references `Folders.Id` |
| Name | TEXT | No | Folder name |
| SortOrder | INTEGER | No | Reserved sort field, default `0` |
| CreatedAt | TEXT | No | ISO 8601 date/time |
| UpdatedAt | TEXT | No | ISO 8601 date/time |

Foreign key:

```text
ParentId -> Folders.Id ON DELETE CASCADE
```

Index:

```text
IX_Folders_ParentId_Name (ParentId, Name)
```

### 9.3 SQLite Table: Prompts

| Column | SQLite Type | Nullable | Description |
| --- | --- | --- | --- |
| Id | INTEGER | No | Primary key, auto-increment |
| FolderId | INTEGER | Yes | Folder ID; references `Folders.Id` |
| Type | TEXT | No | User-defined prompt type, default `Prompt` |
| Mode | TEXT | No | Structured prompt mode, default `Text` |
| Labels | TEXT | No | Semicolon-delimited labels |
| Title | TEXT | No | Prompt title |
| Body | TEXT | No | Prompt body text |
| Status | TEXT | No | `Draft`, `InReview`, `Approved`, or `Retired` |
| Tags | TEXT | No | Plain-text tags |
| Notes | TEXT | No | Plain-text notes |
| FormattedNotes | TEXT | No | Rich notes serialized as RTF text |
| IsFavorite | INTEGER | No | Boolean stored as `0` or `1` |
| IsArchived | INTEGER | No | Boolean stored as `0` or `1` |
| CreatedAt | TEXT | No | ISO 8601 date/time |
| UpdatedAt | TEXT | No | ISO 8601 date/time |
| LastUsedAt | TEXT | Yes | ISO 8601 date/time when copied |

Foreign key:

```text
FolderId -> Folders.Id ON DELETE SET NULL
```

Indexes:

```text
IX_Prompts_Folder_Status (FolderId, Status)
IX_Prompts_Type (Type)
IX_Prompts_Mode (Mode)
IX_Prompts_Labels (Labels)
IX_Prompts_UpdatedAt (UpdatedAt)
```

### 9.4 SQLite Table: PromptTypes

| Column | SQLite Type | Nullable | Description |
| --- | --- | --- | --- |
| Name | TEXT | No | Custom Type value, primary key with case-insensitive comparison |

Prompt Manager inserts `Prompt` by default. When a user saves a prompt with a new Type value, that value is inserted into this table and becomes selectable for later records.

### 9.5 SQLite Table: PromptLabels

| Column | SQLite Type | Nullable | Description |
| --- | --- | --- | --- |
| Name | TEXT | No | Label value, primary key with case-insensitive comparison |

Prompt Manager seeds `Production`, `Approved`, `Training`, and `Deprecated`. When a user saves a prompt with a new label value, that label is inserted into this table and becomes selectable for later records.

### 9.6 SQLite Table: PromptVersions

| Column | SQLite Type | Nullable | Description |
| --- | --- | --- | --- |
| Id | INTEGER | No | Primary key, auto-increment |
| PromptId | INTEGER | No | Prompt ID; references `Prompts.Id` |
| VersionNumber | INTEGER | No | Sequential controlled-record version number |
| FolderId | INTEGER | Yes | Folder at the time of the saved version |
| Type | TEXT | No | Prompt type captured for this version |
| Mode | TEXT | No | Prompt mode captured for this version |
| Labels | TEXT | No | Labels captured for this version |
| Title | TEXT | No | Prompt title captured for this version |
| Body | TEXT | No | Prompt body captured for this version |
| Status | TEXT | No | Lifecycle status captured for this version |
| Tags | TEXT | No | Tags captured for this version |
| Notes | TEXT | No | Plain-text notes captured for this version |
| FormattedNotes | TEXT | No | RTF notes captured for this version |
| ChangeNote | TEXT | No | Version note such as `Initial draft` or `Saved update` |
| CreatedAt | TEXT | No | ISO 8601 date/time |

Foreign key:

```text
PromptId -> Prompts.Id ON DELETE CASCADE
```

### 9.7 SQLite Virtual Table: PromptSearch

When SQLite FTS5 is available, Prompt Manager creates:

```text
PromptSearch
```

FTS columns:

| Column | Source |
| --- | --- |
| Type | `Prompts.Type` |
| Mode | `Prompts.Mode` |
| Labels | `Prompts.Labels` |
| Title | `Prompts.Title` |
| Body | `Prompts.Body` |
| Tags | `Prompts.Tags` |
| Notes | `Prompts.Notes` |

The virtual table is linked to:

```text
content='Prompts'
content_rowid='Id'
```

Triggers maintain the search index:

| Trigger | Event | Purpose |
| --- | --- | --- |
| Prompts_ai | AFTER INSERT | Adds prompt to search index |
| Prompts_ad | AFTER DELETE | Removes prompt from search index |
| Prompts_au | AFTER UPDATE | Refreshes prompt search row |

If FTS5 creation fails, Prompt Manager continues to operate and uses local `LIKE` search instead.

### 9.8 Date, Boolean, and Text Data Types

| Logical Type | Storage |
| --- | --- |
| DateTime | TEXT using round-trip ISO 8601 format |
| Nullable DateTime | TEXT or NULL |
| Boolean | INTEGER, `0` false and `1` true |
| Enum | TEXT enum name |
| Tags | TEXT, user-managed delimiter convention |
| Prompt body | TEXT |
| Type | TEXT, user-managed controlled list with default `Prompt` |
| Mode | TEXT, selected from local structured mode list |
| Labels | TEXT, semicolon-delimited labels with local label catalog |
| Formatted notes | TEXT containing RTF; plain notes remain in `Notes` for search and export readability |

---

## 10. Export Data Types

The JSON export format maps to application DTO classes.

### 10.1 PromptExportPackage

| Property | Type | Description |
| --- | --- | --- |
| Format | string | Must be `PromptManager.Export` |
| FormatVersion | string | Export format version, currently `1.3` |
| ExportedAt | DateTime | Export creation date/time |
| Folders | List of FolderExportDto | Exported folders |
| Prompts | List of PromptExportDto | Exported prompts |

### 10.2 FolderExportDto

| Property | Type | Description |
| --- | --- | --- |
| Id | int | Source folder ID in exported database |
| ParentId | nullable int | Source parent folder ID |
| Name | string | Folder name |

### 10.3 PromptExportDto

| Property | Type | Description |
| --- | --- | --- |
| Id | int | Source prompt ID in exported database |
| FolderId | nullable int | Source folder ID |
| Type | string | Prompt type, default `Prompt` |
| Mode | string | Structured prompt mode, default `Text` |
| Labels | string | Semicolon-delimited labels |
| Title | string | Prompt title |
| Body | string | Prompt body |
| Status | PromptStatus | Prompt lifecycle status |
| Tags | string | Plain-text tags |
| Notes | string | Plain-text notes |
| FormattedNotes | string | RTF-formatted notes |
| IsFavorite | bool | Favorite flag |
| IsArchived | bool | Archive flag |
| CreatedAt | DateTime | Created date/time |
| UpdatedAt | DateTime | Last modified date/time |
| LastUsedAt | nullable DateTime | Last copied date/time |
| Versions | List of PromptVersionExportDto | Version history |

### 10.4 PromptVersionExportDto

| Property | Type | Description |
| --- | --- | --- |
| VersionNumber | int | Version sequence number |
| FolderId | nullable int | Folder ID captured for that version |
| Title | string | Title captured for that version |
| Type | string | Type captured for that version |
| Mode | string | Mode captured for that version |
| Labels | string | Labels captured for that version |
| Body | string | Body text captured for that version |
| Status | string | Lifecycle status captured for that version |
| Tags | string | Tags captured for that version |
| Notes | string | Plain-text notes captured for that version |
| FormattedNotes | string | RTF notes captured for that version |
| ChangeNote | string | Version note |
| CreatedAt | DateTime | Version creation date/time |

---

## 11. Troubleshooting

### 11.1 App Does Not Start

Check:

- .NET 10 Windows Desktop Runtime is installed for development builds, or use the self-contained installer package.
- `PromptManager.exe` exists in the build output folder.
- The Windows user has permission to write to `%LOCALAPPDATA%`.

Run from the project folder:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build PromptManager.sln
```

### 11.2 Search Does Not Return Expected Results

Check:

- The prompt text, title, notes, or tags contain the search term.
- The current view filter is not excluding the prompt.
- The selected folder is not narrowing results too much.

Clear the search box and select `All Prompts` to reset the register.

### 11.3 Prompt Is Missing from All Prompts

The prompt may be archived or retired.

1. Select the `Retired` view.
2. Search for the prompt title.
3. Select the prompt.
4. Click `Restore` if it should return to active use.

### 11.4 Import Creates Duplicate Prompts

Import inserts prompts as new records. It does not merge prompts by title or compare prompt body text. Back up the database before large imports.

### 11.5 Restore Replaced Recent Work

Restore replaces the active database file. If recent work was overwritten, recover it from:

- A newer backup file, if available.
- A JSON export, if available.
- Windows file backup or OneDrive version history, if available for the backup location.

### 11.6 `dotnet` Is Not Recognized

The SDK may be installed but not on PATH. Try:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' --info
```

If that works, add this folder to PATH:

```text
C:\Program Files\dotnet\
```

---

## 12. Administrative Notes

### 12.1 Local Database Path

The active database is:

```text
%LOCALAPPDATA%\PromptManager\prompt-manager.db
```

Expanded example:

```text
C:\Users\<UserName>\AppData\Local\PromptManager\prompt-manager.db
```

### 12.2 Moving Data to Another PC

Recommended methods:

1. Use `Backup` on the source PC and `Restore` on the target PC.
2. Use `Export` on the source PC and `Import` on the target PC.

Backup/restore is best for full replacement. Export/import is best for sharing prompt sets.

### 12.3 Suggested Backup Schedule

| Library Usage | Suggested Backup Frequency |
| --- | --- |
| Occasional use | Before and after major edits |
| Weekly use | Weekly |
| Daily use | Daily or before bulk imports |
| Controlled library | Before review cycles and before releases |

### 12.4 Current Verification Status

The current build has been smoke-tested with the following results:

| Check | Result |
| --- | --- |
| Solution build | Passed |
| Main window launch | Passed |
| Expected controls visible | Passed |
| Local database creation | Passed |
| Seed prompts created | Passed |
| FTS search row creation | Passed |
| FTS `CSV*` query | Passed |

---

## 13. Glossary

| Term | Meaning |
| --- | --- |
| Archive | Hide a prompt from active views and mark it retired |
| Backup | Copy the complete SQLite database to a selected file |
| Delete Folder | Remove a folder branch while preserving contained prompts as Unfiled |
| Export | Write folder and prompt data to a JSON file |
| FTS | Full-text search |
| Prompt Body | The reusable prompt text copied for use elsewhere |
| Restore | Replace the active database from a backup file |
| Version | A saved copy of prompt body text |

---

## 14. Quick Reference

| Task | Action |
| --- | --- |
| Create folder | Select parent folder, click `New Folder` |
| Delete folder | Select folder, click `Delete Folder` |
| Create prompt | Select folder, click `New Prompt` |
| Move prompt | Select prompt, choose `Path`, click `Save` |
| Save changes | Edit Properties panel, click `Save` |
| Copy prompt | Select prompt, click `Copy` or double-click row |
| Mark favorite | Select prompt, click `Favorite` |
| Archive prompt | Select prompt, click `Archive` |
| Restore prompt | Open `Retired`, select prompt, click `Restore` |
| Search | Type in top search box |
| Export | Click `Export`, choose `.json` destination |
| Import | Click `Import`, select `.json` export |
| Backup | Click `Backup`, choose `.db` destination |
| Restore database | Click `Restore`, select `.db` backup |







