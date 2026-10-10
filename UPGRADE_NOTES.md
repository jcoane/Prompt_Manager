# Prompt Manager 1.4.0 Upgrade Notes

Date: 2026-10-10. Status: development release, technical verification; not formal validation or quality approval.
Active project: `D:\dev\Prompt_Manager`. Delivery: `artifacts\installer\PromptManagerSetup.exe`.

## Changes

- Left-pointing flaming-arrow application, Explorer, window, Setup, and shortcut icons; embedded multiresolution ICO with 16 through 256 pixel variants. ProtoCall About logo remains.
- Compact File menu, folder toolbar, optional/persistent register columns and sorting, numeric version sorting, In Review view, explicit unsaved indicator.
- Larger full-height Notes and Prompt editors; expand to a larger window; reading-size control; bold, italic, and color swatches.
- Folder rename and move, descendant-cycle prevention, duplicate sibling checks, batch prompt moves with atomic record updates.
- Selectable revision snapshots, metadata and ordered line differences, restore older content as a new Draft revision without dropping newer history.
- Local variable filling, preview, and copying; explicit selected/current-view/library export scopes.
- Edit revision checks prevent stale overwrites; archive, restore, and folder deletion relocations create versions.
- Historical import fields are not replaced with current values; current and historical folder references are independently remapped. Invalid imports roll back.
- SQLite snapshot backup includes committed WAL content. Restore validates and migrates a staged candidate before replacement and makes a unique safety backup. Schema migration makes a pre-upgrade backup.
- No automatic sample reseeding when a library is empty. Startup errors are reported without deleting data; per-user/session single-instance guard.
- Installer refuses to force-close a running app; payload-file replacement rolls back on copy failures and preserves unrelated files. Existing registered install locations are used.
- Rendered offline HTML user manual plus updated Markdown edition, file/data reference, recovery guidance, and About version.

## Verification

- .NET 10.0.401 SDK on Windows; app targets net10.0-windows.
- Release solution build: zero errors and zero warnings in the final completed run.
- Original AboutManualSmoke, FolderRefreshSmoke, PromptMoveSmoke, TypeNotesSmoke: passed.
- UpgradeSafetySmoke: 69 checks passed. Covers normal operations and simulated boundary/failure fixtures for stale saves, invalid restore/schema versions, WAL backup, folder cycles, import rollback/history fidelity, batch move failure, detached-history recovery, installer rollback/path traversal, variable substitution, repeated-line diff, preferences, and real WPF Notes formatting/save controls.
- WPF layouts checked and rendered at 1480x880 and 1000x620. Notes and Prompt remain contained and usable; opening Notes does not create false pending edits. Rendered images were visually inspected.
- Thirteen package/install checks passed: self-contained runtime/native SQLite/manual/logo payload, executable version, ten-frame ICO, full temporary custom-directory install and repeat update, manifest/manual creation, unrelated file preservation, and embedded Explorer icon pixel comparison for both executables.
- Existing-library compatibility verified on a SQLite snapshot copy before installation. Recovered history loaded after migration; the compatibility test did not migrate the original library.
- Authorized detached-history recovery preserved current prompt and folder fields and created a safety snapshot. Post-recovery database validation passed. Machine-specific evidence and backup filenames remain in local verification records, not this public source repository.
- Existing Program Files (x86) installation updated successfully to 1.4.0; the installed application launched with the existing library. Original prompt, folder, version, and recovery fields were compared against a fresh pre-install snapshot and were unchanged after schema migration. Database integrity and relationships passed. Desktop and Start menu shortcuts and the bundled manual were verified.
- Tests otherwise use temporary libraries. The test-only App constructor disables production-library startup while WPF controls are exercised.

## Packaging

`tools\Build-Installer.ps1` publishes the self-contained application, creates its payload ZIP, and publishes the self-contained single-file Setup executable. It checks required application/manual/logo files and prints the resulting executable version, size, and SHA-256.

Final installer: `D:\dev\Prompt_Manager\artifacts\installer\PromptManagerSetup.exe`.
File version: `1.4.0.0`. Size: `118892918` bytes.
SHA-256: `90CBA4B859A01B3334E51750E668B9971FD2EDFC2F166D8293F9AE767424EECC`.
The 13 package/install checks were repeated against this final package and passed.

Final verification logs and WPF renderings are retained locally in `artifacts\verification\` (generated files excluded from version control).

The application remains offline: no network features, accounts, model execution, telemetry, hosting, or encryption were added. Dependencies may need restoring in a developer environment, but installed operation does not use them over a network.

## Remaining Limits

- The installed Program Files copy is not automatically replaced by building. Run the new installer with the app closed to update it and recreate shortcuts. Windows may cache previously pinned icons.
- No Authenticode signing, formal approval workflow, electronic signatures, multi-user collaboration, or folder audit log is implemented. Approved remains a user-selected classification.
- Installer file-update rollback covers the payload; failures in later shortcut/registry registration may require rerunning Setup. An in-place Program Files upgrade was verified with Windows administrator approval. Behavior across alternative Windows users and a full unattended system-install/uninstall cycle have not been verified.
- Large line comparisons are bounded to one million matrix cells; complete saved/current text remains accessible. Large-library performance is not benchmarked. Inline metadata comparisons do not semantically interpret RTF.
- Pre-recovery backups intentionally retain original detached relationships and require recovery before normal restore validation accepts them. Entire-library JSON export and SQLite backup retain recovered entries; scoped exports exclude unrelated recovery evidence.
- Import adds records rather than merging; repeated imports may duplicate them. The tool is not a replacement for controlled document procedures or validation evidence.

Future priorities: a mature signed installer/uninstaller, stronger automated startup/upgrade integration tests, paging/asynchronous search for very large libraries, and optional configurable retention/archive review workflows.
