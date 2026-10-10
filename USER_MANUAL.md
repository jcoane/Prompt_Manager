# Prompt Manager User Manual

**Version:** 1.4.0

**Platform:** Standalone Windows 11 desktop, x64

**Author:** John-Patrick Coane

**Storage:** Local SQLite; offline operation

The professionally styled version of this guide is `USER_MANUAL.html`, opened by Help > User Manual. Both editions are included in Setup. This guide describes software operation; it is not a quality approval, electronic signature system, or formal validation package.

## Installation and Starting

1. Save edits and close Prompt Manager. Setup will not force-close a running process.
2. Run `PromptManagerSetup.exe` and accept the Windows administrator prompt when appropriate.
3. Choose the destination. The default is `C:\Program Files (x86)\Prompt Manager`; Browse allows another location. A registered existing installation is used when detected.
4. Choose Desktop and Start menu shortcut options, then Install.
5. Start from the shortcut, Start menu entry, or `PromptManager.exe` in the installation directory.

The installation package includes the .NET desktop runtime, SQLite components, logo, and manual. A separate runtime download is not required. Installation files are separate from `%LOCALAPPDATA%\PromptManager\prompt-manager.db`. Back up before upgrading. Opening an older schema creates a pre-upgrade database backup before migration. New libraries start empty; deleting every record does not recreate sample records.

## Workspace and Register

The left pane contains Views and Directories, the center contains the register, and the right pane contains the editor. Search and New Prompt remain prominent. File contains Import, explicit Export scopes, Backup Library, and Restore Library.

Views: All Prompts, Favorites, Recently Used, Drafts, In Review, Approved, and Retired. All Prompts excludes archived records. Retired includes archived records and records marked Retired. A folder selection includes its descendants.

The register initially shows Name, Status, Type, Tags, Version, and Modified. Columns allows optional Mode, Labels, Path, and Favorite. Name stays visible. Click a column header to sort, resize or reorder columns, and drag pane dividers. Columns, widths, sorting, pane sizes, and reading size persist locally after a normal close. Version sorting is numeric.

Double-click a prompt to open its Prompt tab. Ctrl+F focuses Search, Ctrl+N creates a prompt, and Ctrl+S saves. Hover over icon buttons for their action names.

## Creating, Editing, and Saving

1. Select a directory, then New Prompt. A Draft is created and selected. Any active search is cleared.
2. Enter a specific Title. The editable Type list defaults to Prompt; custom values become selectable after saving.
3. Choose Mode, Path, Status, and optional Labels; enter Tags.
4. Edit Prompt text and formatted Notes. Unsaved identifies pending edits.
5. Save and enter a meaningful change note when requested. Controlled changes create a new snapshot revision.

| Field | Purpose |
| --- | --- |
| Title | Required nonblank record name |
| Type | Reusable custom classification; default Prompt |
| Mode | Text, Chat, System/User, Checklist, Matrix, Protocol |
| Path | Containing folder; Unfiled means none; changes take effect on Save |
| Tags | Searchable free text |
| Labels | Catalog values, delimited by semicolons or commas |
| Status | Draft, InReview, Approved, Retired |
| Notes | Searchable plain text plus RTF formatting |
| Prompt | Reusable multiline plain text |

Template inserts a structure for the selected Mode; existing text is appended to only after confirmation. Changes of view, directory, search, selection, or closing the main window request Save, Discard, or Cancel when edits are pending. Cancel retains the editor.

A conflicting save is rejected if the stored revision changed since the record was opened. Your editor text is retained. Copy it out before refreshing, review the current record, and reconcile the changes. The application is a single-user tool, not shared multi-user collaboration software.

## Directories and Moving

Use the directory toolbar to create, rename, move, or delete a folder. Select All Folders for a new top-level folder; otherwise creation is beneath the selected folder. Blank names, duplicate sibling names, and circular moves are rejected. Rename/move refreshes current displayed paths.

Move one prompt with Details > Path and Save. Move several with Ctrl-click or Shift-click in the register and its move icon. Batch moves commit together; each changed prompt receives a history entry. The register refreshes to the destination.

Folder deletion removes its descendants but moves contained prompts to Unfiled. It does not delete prompts; relocation is versioned. Folder rename/parent changes do not create a separate folder audit log. Permanent prompt deletion removes the prompt and its history. Archive is the recoverable alternative.

## Notes and Reading

Notes fills the available editor height. Select text and use B, I, or the color swatches. Formatting is stored as RTF alongside searchable plain text. Reading size changes presentation, not the saved revision.

Expand on Notes or Prompt opens the editor in a larger window. Done returns to the main window retaining edits; it does not save them. Use Save afterward. Notes and Prompt text scroll independently.

## Review and Versions

Review provides advisory checks for missing titles, tags, labels, template sections, unfinished placeholders, and possible duplicates. These are heuristics, not scientific correctness checks or compliance determinations. Approved is a user-selected classification, not formal authorization.

Versions lists saved revisions, times, and change notes. Select a revision for Saved Text and Differences against the current editor. Differences include available metadata and ordered lines, including blank/repeated lines. Large comparisons are bounded; full saved and current text remain available separately. Older revisions may lack metadata and are identified as not recorded rather than being filled with current values.

Restore Revision as Draft asks for a reason and appends a new Draft revision. Newer history remains. Historical approval is not reinstated. A historical folder that no longer exists becomes Unfiled. Archive and restore from archive create history; favorite and copy/usage changes do not.

## Variables and Copying

Variables use `{{Name}}`. Names begin with a letter or underscore, followed by letters, numbers, underscores, dots, or hyphens. Insert Variable inserts at the cursor.

Fill Variables and Copy opens local value fields and a preview. Every detected variable requires a nonblank value; repeated names are filled consistently. Copy does not modify the stored template or execute an AI model. The ordinary copy icon copies the current text, including unsaved edits, without saving them. Other applications and Windows Clipboard History may retain clipboard data.

## Search

Search covers Type, Mode, Labels, Title, Body, Tags, and plain Notes, combined with the active view and folder. FTS5 uses prefix terms; a local text-search fallback is available when FTS is not. For missing results, clear Search, select All Folders, and check All Prompts and Retired.

## Import and Export

File > Export offers Selected Prompts, Current View, or Entire Library. Exports contain saved records and history. Current View means the displayed result set. Entire Library includes archived records, all folders, and recovered history. Scoped exports include required folder ancestors and historical references, but not unrelated recovery entries.

JSON format is `PromptManager.Export`, version `1.4`. Supported 1.x formats through 1.4 may be imported. Invalid records, missing parents, cycles, and unsupported formats are rejected. Import remaps current and historical folder IDs and commits as a transaction. Historical empty fields stay empty. Import adds new prompts; it does not merge or deduplicate automatically. Repeating an import can duplicate prompts or recovery entries.

## Backup, Restore, and Recovery

Choose File > Backup Library and a destination separate from the active database. SQLite snapshot backup includes committed write-ahead-log data. Keep backups in an approved, separate location and periodically test restoration using a non-production copy.

Restore replaces the entire library, not just a view. Save pending work, close other tools using the database, select the backup, and confirm. The candidate is integrity/schema/relationship checked and migrated on a temporary copy. A unique pre-restore backup preserves the previous library, then the staged database replaces it. Candidate validation failures do not replace the active database.

If legacy history has no matching prompt, startup offers recovery. Recovery creates a pre-recovery snapshot and moves each detached version into `RecoveryHistory`, retaining original IDs and every original field in JSON. Current prompts and folders are not changed. Help > Recovery Records displays these entries read-only. Normal backups and Entire Library exports include them. A pre-recovery database retains the original broken relationships and must be repaired before a normal restore is accepted.

## Files and Data Types

```text
Installation directory (user selectable)
  PromptManager.exe
  Runtime and application dependencies
  Assets\ProtoCall_logo.png
  USER_MANUAL.html
  USER_MANUAL.md
  install-manifest.txt
  UninstallPromptManager.ps1
  Uninstall Prompt Manager.cmd

%LOCALAPPDATA%\PromptManager\
  prompt-manager.db
  ui-settings.json
  pre-upgrade-*.db
  pre-restore-backup-*.db
  pre-recovery-*.db
  prompt-manager.db-wal / prompt-manager.db-shm (if WAL is active)
```

The left-pointing flaming-arrow icon is embedded in the application, window resources, Setup, and shortcut targets. About retains the ProtoCall logo and author attribution.

| Table | Data types and principal fields |
| --- | --- |
| Folders | Id INTEGER key; ParentId nullable INTEGER; Name TEXT; SortOrder INTEGER; timestamps ISO date-time TEXT |
| Prompts | Id/Revision INTEGER; FolderId nullable INTEGER; Title/Type/Mode/Labels/Body/Status/Tags/Notes/FormattedNotes TEXT; favorite/archive INTEGER 0/1; timestamps TEXT; LastUsedAt nullable |
| PromptVersions | Id/PromptId/VersionNumber INTEGER; controlled fields TEXT snapshots; FolderId nullable INTEGER; FolderPath historic TEXT; IsArchived INTEGER; ChangeNote and CreatedAt TEXT |
| PromptTypes / PromptLabels | Name case-insensitive TEXT catalog key |
| RecoveryHistory | Id INTEGER key; OriginalVersionId/OriginalPromptId/VersionNumber INTEGER; SnapshotJson/RecoveredAt/Reason TEXT |
| PromptSearch | Derived FTS5 index maintained by prompt insert/update/delete triggers |

Schema version is `PRAGMA user_version = 4`; application ID is `0x504D4752`. JSON uses integer IDs, boolean flags, string text/date-times, nullable directory IDs, and arrays. Status enum values in exports are Draft=0, InReview=1, Approved=2, Retired=3. Recovery snapshots retain original SQLite field values. Folder nesting is limited to 256 levels. UI settings contain column, sort, pane, and reading-size preferences.

## Troubleshooting and Limits

- Setup blocked: save and close all Prompt Manager processes, then retry. It never kills them.
- Conflict: retain/copy your edits, refresh, and reconcile against the stored record.
- Restore rejected: select a compatible complete backup; keep the current library unchanged.
- Manual absent: rerun Setup. Help prefers HTML and falls back to Markdown.
- Icon stale: recreate shortcuts with Setup; Windows may retain cached or previously pinned icons.
- Already running: use the taskbar window. Version 1.4 prevents a second instance for the same user/session.
- Recovery entries are historical evidence, not current approved prompts; do not silently reinterpret them.

The application has no login, cloud connectivity, synchronization, telemetry, hosted collaboration, online AI execution, API hosting, encryption, or electronic signatures. Protect local libraries, backups, exports, and clipboard contents according to their sensitivity. Do not place confidential files in a synchronized folder without authorization. Technical testing does not establish formal validation or regulatory acceptance.
