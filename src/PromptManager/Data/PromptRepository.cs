using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PromptManager.Models;

namespace PromptManager.Data;

public sealed class PromptRepository
{
    private const int SchemaVersion = 4;
    private const int ApplicationId = 0x504D4752;
    private bool _isFtsAvailable;

    public PromptRepository(string? databasePath = null)
    {
        if (!string.IsNullOrWhiteSpace(databasePath))
        {
            DatabasePath = databasePath;
            return;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        DatabasePath = Path.Combine(appData, "PromptManager", "prompt-manager.db");
    }

    public string DatabasePath { get; }

    public void Initialize(bool createUpgradeBackup = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        if (File.Exists(DatabasePath) && new FileInfo(DatabasePath).Length > 0)
            ValidateBackupDatabase(DatabasePath);
        using var connection = OpenConnection();
        if (ReadPragma(connection, "user_version") > SchemaVersion)
            throw new InvalidOperationException("This library was created by a newer version of Prompt Manager.");
        if (createUpgradeBackup && new FileInfo(DatabasePath).Length > 0 && ReadPragma(connection, "user_version") < SchemaVersion)
            CopyDatabaseSnapshot(DatabasePath, Path.Combine(Path.GetDirectoryName(DatabasePath)!, "pre-upgrade-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8] + ".db"));
        CreateSchema(connection);
        MigrateSchema(connection);
        SeedPromptTypes(connection);
        SeedPromptLabels(connection);
        _isFtsAvailable = TryEnsureFullTextSearch(connection);

        ExecuteNonQuery(connection, $"PRAGMA user_version = {SchemaVersion}; PRAGMA application_id = {ApplicationId};");
    }

    public void AddExamples()
    {
        using var connection = OpenConnection();
        SeedIfEmpty(connection);
    }

    public int DetachedHistoryCount()
    {
        if (!File.Exists(DatabasePath)) return 0;
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        if (!ColumnExists(connection, "PromptVersions", "PromptId") || !ColumnExists(connection, "Prompts", "Id")) return 0;
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM PromptVersions WHERE PromptId NOT IN (SELECT Id FROM Prompts);";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public string? RecoverDetachedHistory()
    {
        ValidateBackupDatabase(DatabasePath, allowDetachedHistory: true);
        if (DetachedHistoryCount() == 0) return null;
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var safety = Path.Combine(Path.GetDirectoryName(DatabasePath)!, "pre-recovery-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8] + ".db");
        CopyDatabaseSnapshot(DatabasePath, safety);
        CreateRecoveryTable(connection, transaction);
        var records = new List<Dictionary<string, object?>>();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT * FROM PromptVersions WHERE PromptId NOT IN (SELECT Id FROM Prompts);";
            using var reader = command.ExecuteReader();
            while (reader.Read())
                records.Add(Enumerable.Range(0, reader.FieldCount).ToDictionary(reader.GetName, i => reader.IsDBNull(i) ? null : reader.GetValue(i)));
        }
        foreach (var record in records)
        {
            InsertRecoveryRecord(connection, transaction, new RecoveryHistoryDto { OriginalVersionId = Convert.ToInt64(record["Id"]),
                OriginalPromptId = Convert.ToInt64(record["PromptId"]), VersionNumber = Convert.ToInt32(record["VersionNumber"]),
                SnapshotJson = JsonSerializer.Serialize(record), RecoveredAt = DateTime.Now,
                Reason = "Detached historical version: no matching prompt. Original fields retained during relationship repair." });
            using var delete = connection.CreateCommand();
            delete.Transaction = transaction; delete.CommandText = "DELETE FROM PromptVersions WHERE Id=$id;";
            delete.Parameters.AddWithValue("$id", record["Id"]!); delete.ExecuteNonQuery();
        }
        transaction.Commit();
        return safety;
    }

    public IReadOnlyList<RecoveryHistoryDto> GetRecoveryHistory()
    {
        using var connection = OpenConnection();
        if (!ColumnExists(connection, "RecoveryHistory", "SnapshotJson")) return Array.Empty<RecoveryHistoryDto>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT OriginalVersionId,OriginalPromptId,VersionNumber,SnapshotJson,RecoveredAt,Reason FROM RecoveryHistory ORDER BY Id;";
        using var reader = command.ExecuteReader(); var records = new List<RecoveryHistoryDto>();
        while (reader.Read()) records.Add(new RecoveryHistoryDto { OriginalVersionId=reader.GetInt64(0),OriginalPromptId=reader.GetInt64(1),
            VersionNumber=reader.GetInt32(2),SnapshotJson=reader.GetString(3),RecoveredAt=FromDatabaseDate(reader.GetString(4))!.Value,Reason=reader.GetString(5) });
        return records;
    }

    private static void CreateRecoveryTable(SqliteConnection connection, SqliteTransaction? transaction = null) => ExecuteNonQuery(connection,
        "CREATE TABLE IF NOT EXISTS RecoveryHistory (Id INTEGER PRIMARY KEY AUTOINCREMENT, OriginalVersionId INTEGER NOT NULL, OriginalPromptId INTEGER NOT NULL, VersionNumber INTEGER NOT NULL, SnapshotJson TEXT NOT NULL, RecoveredAt TEXT NOT NULL, Reason TEXT NOT NULL);", transaction);

    private static void InsertRecoveryRecord(SqliteConnection connection, SqliteTransaction transaction, RecoveryHistoryDto record)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "INSERT INTO RecoveryHistory (OriginalVersionId,OriginalPromptId,VersionNumber,SnapshotJson,RecoveredAt,Reason) VALUES ($versionId,$promptId,$number,$json,$time,$reason);";
        command.Parameters.AddWithValue("$versionId",record.OriginalVersionId); command.Parameters.AddWithValue("$promptId",record.OriginalPromptId);
        command.Parameters.AddWithValue("$number",record.VersionNumber); command.Parameters.AddWithValue("$json",record.SnapshotJson);
        command.Parameters.AddWithValue("$time",ToDatabaseDate(record.RecoveredAt)); command.Parameters.AddWithValue("$reason",record.Reason); command.ExecuteNonQuery();
    }

    public IReadOnlyList<FolderNode> LoadFolderTree()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, ParentId, Name FROM Folders ORDER BY ParentId, Name;";

        var nodes = new List<FolderNode>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            nodes.Add(new FolderNode(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetInt32(1),
                reader.GetString(2)));
        }

        var byId = nodes.ToDictionary(node => node.Id);
        var roots = new List<FolderNode>();

        foreach (var node in nodes)
        {
            if (node.ParentId.HasValue && byId.TryGetValue(node.ParentId.Value, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        return roots;
    }

    public Dictionary<PromptFilterKind, int> GetFilterCounts()
    {
        using var connection = OpenConnection();

        return new Dictionary<PromptFilterKind, int>
        {
            [PromptFilterKind.All] = CountPrompts(connection, "IsArchived = 0"),
            [PromptFilterKind.Favorites] = CountPrompts(connection, "IsFavorite = 1 AND IsArchived = 0"),
            [PromptFilterKind.RecentlyUsed] = CountPrompts(connection, "LastUsedAt IS NOT NULL AND IsArchived = 0"),
            [PromptFilterKind.Drafts] = CountPrompts(connection, "Status = 'Draft' AND IsArchived = 0"),
            [PromptFilterKind.InReview] = CountPrompts(connection, "Status = 'InReview' AND IsArchived = 0"),
            [PromptFilterKind.Approved] = CountPrompts(connection, "Status = 'Approved' AND IsArchived = 0"),
            [PromptFilterKind.Retired] = CountPrompts(connection, "Status = 'Retired' OR IsArchived = 1")
        };
    }

    public IReadOnlyList<string> GetPromptTypes()
    {
        using var connection = OpenConnection();
        var types = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase)
        {
            "Prompt"
        };

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT Name FROM PromptTypes
                UNION
                SELECT Type FROM Prompts
                ORDER BY Name;
                """;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var type = NormalizePromptType(reader.IsDBNull(0) ? null : reader.GetString(0));
                types.Add(type);
            }
        }

        return types
            .OrderBy(type => string.Equals(type, "Prompt", StringComparison.CurrentCultureIgnoreCase) ? 0 : 1)
            .ThenBy(type => type, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetPromptLabels()
    {
        using var connection = OpenConnection();
        var labels = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase)
        {
            "Production",
            "Approved",
            "Training",
            "Deprecated"
        };

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT Name FROM PromptLabels ORDER BY Name;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var label = reader.IsDBNull(0) ? "" : reader.GetString(0).Trim();
                if (!string.IsNullOrWhiteSpace(label))
                {
                    labels.Add(label);
                }
            }
        }

        return labels.ToList();
    }

    public IReadOnlyList<PromptRow> GetPrompts(PromptFilterKind filter, int? folderId, string? searchText)
    {
        using var connection = OpenConnection();
        var folderPaths = GetFolderPathMap(connection);
        var ftsQuery = BuildFtsQuery(searchText);
        var useFts = _isFtsAvailable && !string.IsNullOrWhiteSpace(ftsQuery);
        var where = new List<string>();

        var sql =
            "SELECT p.Id, p.FolderId, p.Type, p.Mode, p.Labels, p.Title, p.Body, p.Status, p.Tags, p.Notes, p.FormattedNotes, p.IsFavorite, p.IsArchived, " +
            "p.CreatedAt, p.UpdatedAt, p.LastUsedAt, COALESCE(v.Version, 1) AS Version, p.Revision " +
            "FROM Prompts p ";

        if (useFts)
        {
            sql += "JOIN PromptSearch s ON s.rowid = p.Id ";
        }

        sql += "LEFT JOIN (SELECT PromptId, MAX(VersionNumber) AS Version FROM PromptVersions GROUP BY PromptId) v ON v.PromptId = p.Id ";

        using var command = connection.CreateCommand();
        ApplyFilter(where, filter);

        if (folderId.HasValue)
        {
            var folderIds = GetDescendantFolderIds(connection, folderId.Value);
            var parameterNames = new List<string>();
            for (var i = 0; i < folderIds.Count; i++)
            {
                var parameterName = "$folder" + i.ToString(CultureInfo.InvariantCulture);
                parameterNames.Add(parameterName);
                command.Parameters.AddWithValue(parameterName, folderIds[i]);
            }

            where.Add("p.FolderId IN (" + string.Join(", ", parameterNames) + ")");
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            if (useFts)
            {
                where.Add("PromptSearch MATCH $search");
                command.Parameters.AddWithValue("$search", ftsQuery);
            }
            else
            {
                where.Add("(p.Type LIKE $like OR p.Mode LIKE $like OR p.Labels LIKE $like OR p.Title LIKE $like OR p.Body LIKE $like OR p.Tags LIKE $like OR p.Notes LIKE $like)");
                command.Parameters.AddWithValue("$like", "%" + searchText.Trim() + "%");
            }
        }

        if (where.Count > 0)
        {
            sql += "WHERE " + string.Join(" AND ", where) + " ";
        }

        sql += filter == PromptFilterKind.RecentlyUsed
            ? "ORDER BY p.LastUsedAt DESC, p.Title;"
            : "ORDER BY p.UpdatedAt DESC, p.Title;";

        command.CommandText = sql;
        return ReadPrompts(command, folderPaths);
    }

    public PromptRow? GetPromptById(int promptId)
    {
        using var connection = OpenConnection();
        var folderPaths = GetFolderPathMap(connection);
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT p.Id, p.FolderId, p.Type, p.Mode, p.Labels, p.Title, p.Body, p.Status, p.Tags, p.Notes, p.FormattedNotes, p.IsFavorite, p.IsArchived, " +
            "p.CreatedAt, p.UpdatedAt, p.LastUsedAt, COALESCE(v.Version, 1) AS Version, p.Revision " +
            "FROM Prompts p " +
            "LEFT JOIN (SELECT PromptId, MAX(VersionNumber) AS Version FROM PromptVersions GROUP BY PromptId) v ON v.PromptId = p.Id " +
            "WHERE p.Id = $id;";
        command.Parameters.AddWithValue("$id", promptId);
        return ReadPrompts(command, folderPaths).FirstOrDefault();
    }

    public string GetCurrentPromptBody(int promptId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Body FROM Prompts WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", promptId);
        return command.ExecuteScalar() as string ?? "";
    }

    public bool HasControlledRecordChanges(PromptRow prompt)
    {
        using var connection = OpenConnection();
        var promptType = NormalizePromptType(prompt.Type);
        var promptMode = NormalizePromptMode(prompt.Mode);
        var labels = NormalizeLabels(prompt.Labels);
        return HasControlledRecordChanges(connection, null, prompt, promptType, promptMode, labels);
    }

    public IReadOnlyList<string> GetDuplicateWarnings(int promptId, string title, string body)
    {
        using var connection = OpenConnection();
        var warnings = new List<string>();
        var normalizedTitle = title.Trim().ToLowerInvariant();
        var normalizedBody = body.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(normalizedTitle))
        {
            var matches = FindPromptTitles(connection, promptId, "LOWER(TRIM(Title)) = $value", normalizedTitle);
            foreach (var match in matches)
            {
                warnings.Add("Same title as: " + match);
            }
        }

        if (normalizedBody.Length >= 40)
        {
            var matches = FindPromptTitles(connection, promptId, "LOWER(TRIM(Body)) = $value", normalizedBody);
            foreach (var match in matches)
            {
                warnings.Add("Same prompt text as: " + match);
            }
        }

        return warnings;
    }

    public int CreateFolder(int? parentId, string folderName)
    {
        var now = DateTime.Now;
        using var connection = OpenConnection();
        ValidateFolderName(connection, parentId, folderName, null);
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO Folders (ParentId, Name, SortOrder, CreatedAt, UpdatedAt) " +
            "VALUES ($parentId, $name, 0, $createdAt, $updatedAt); " +
            "SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$parentId", parentId.HasValue ? parentId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$name", folderName.Trim());
        command.Parameters.AddWithValue("$createdAt", ToDatabaseDate(now));
        command.Parameters.AddWithValue("$updatedAt", ToDatabaseDate(now));
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public void UpdateFolder(int folderId, string name, int? parentId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        if (parentId.HasValue && GetDescendantFolderIds(connection, folderId, transaction).Contains(parentId.Value))
            throw new InvalidOperationException("A folder cannot be moved into itself or one of its subfolders.");
        ValidateFolderName(connection, parentId, name, folderId, transaction);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE Folders SET Name=$name, ParentId=$parent, UpdatedAt=$date WHERE Id=$id;";
        command.Parameters.AddWithValue("$name", name.Trim());
        command.Parameters.AddWithValue("$parent", parentId.HasValue ? parentId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$date", ToDatabaseDate(DateTime.Now));
        command.Parameters.AddWithValue("$id", folderId);
        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("The folder no longer exists.");
        transaction.Commit();
    }

    private static void ValidateFolderName(SqliteConnection connection, int? parentId, string name, int? excludeId, SqliteTransaction? transaction = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Enter a folder name.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM Folders WHERE Name=$name COLLATE NOCASE AND Id<>$id AND ((ParentId IS NULL AND $parent IS NULL) OR ParentId=$parent);";
        command.Parameters.AddWithValue("$name", name.Trim());
        command.Parameters.AddWithValue("$parent", parentId.HasValue ? parentId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$id", excludeId ?? -1);
        if (Convert.ToInt32(command.ExecuteScalar()) > 0)
            throw new InvalidOperationException("A folder with this name already exists in the selected location.");
    }

    public void DeleteFolder(int folderId)
    {
        using var connection = OpenConnection();
        var folderIds = GetDescendantFolderIds(connection, folderId);
        if (folderIds.Count == 0)
        {
            return;
        }

        var promptsToMove = GetPrompts(PromptFilterKind.All, null, null).Concat(GetPrompts(PromptFilterKind.Retired, null, null))
            .Where(prompt => prompt.FolderId.HasValue && folderIds.Contains(prompt.FolderId.Value))
            .DistinctBy(prompt => prompt.Id).ToList();
        using var transaction = connection.BeginTransaction();

        foreach (var prompt in promptsToMove)
        {
            prompt.FolderId = null;
            SavePromptRecord(connection, transaction, prompt, "Moved to Unfiled because the containing folder was deleted");
        }

        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM Folders WHERE Id = $id;";
            deleteCommand.Parameters.AddWithValue("$id", folderId);
            deleteCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public int CreatePrompt(int? folderId)
    {
        var now = DateTime.Now;
        const string body = "Purpose:\r\n\r\nInstructions:\r\n\r\nInputs:\r\n\r\nExpected Output:\r\n";

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "INSERT INTO Prompts (FolderId, Type, Mode, Labels, Title, Body, Status, Tags, Notes, FormattedNotes, IsFavorite, IsArchived, CreatedAt, UpdatedAt, LastUsedAt) " +
            "VALUES ($folderId, 'Prompt', 'Text', '', $title, $body, 'Draft', '', '', '', 0, 0, $createdAt, $updatedAt, NULL); " +
            "SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$folderId", folderId.HasValue ? folderId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$title", "Untitled Prompt");
        command.Parameters.AddWithValue("$body", body);
        command.Parameters.AddWithValue("$createdAt", ToDatabaseDate(now));
        command.Parameters.AddWithValue("$updatedAt", ToDatabaseDate(now));

        var promptId = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        InsertPromptVersion(connection, transaction, promptId, 1, folderId, "Untitled Prompt", "Prompt", "Text", "", body, "Draft", "", "", "", "Initial draft", now);
        transaction.Commit();
        return promptId;
    }

    public PromptRow SavePrompt(PromptRow prompt)
    {
        return SavePrompt(prompt, "Saved update");
    }

    public PromptRow SavePrompt(PromptRow prompt, string changeNote)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        SavePromptRecord(connection, transaction, prompt, changeNote);
        transaction.Commit();
        return GetPromptById(prompt.Id) ?? throw new InvalidOperationException("The prompt no longer exists.");
    }

    private static void SavePromptRecord(SqliteConnection connection, SqliteTransaction transaction, PromptRow prompt, string changeNote)
    {
        if (string.IsNullOrWhiteSpace(prompt.Title)) throw new InvalidOperationException("Enter a prompt title before saving.");
        var now = DateTime.Now;
        var normalizedChangeNote = string.IsNullOrWhiteSpace(changeNote) ? "Saved update" : changeNote.Trim();

        var promptType = NormalizePromptType(prompt.Type);
        EnsurePromptType(connection, transaction, promptType);
        var promptMode = NormalizePromptMode(prompt.Mode);
        var labels = NormalizeLabels(prompt.Labels);
        EnsurePromptLabels(connection, transaction, labels);

        var recordChanged = HasControlledRecordChanges(connection, transaction, prompt, promptType, promptMode, labels);
        var currentVersion = GetCurrentVersion(connection, transaction, prompt.Id);

        using (var updateCommand = connection.CreateCommand())
        {
            updateCommand.Transaction = transaction;
            updateCommand.CommandText =
                "UPDATE Prompts SET FolderId = $folderId, Type = $type, Mode = $mode, Labels = $labels, Title = $title, Body = $body, Status = $status, Tags = $tags, Notes = $notes, FormattedNotes = $formattedNotes, " +
                "IsFavorite = $isFavorite, IsArchived = $isArchived, UpdatedAt = $updatedAt, Revision = Revision + 1 WHERE Id = $id AND Revision = $revision;";
            updateCommand.Parameters.AddWithValue("$id", prompt.Id);
            updateCommand.Parameters.AddWithValue("$revision", prompt.Revision);
            updateCommand.Parameters.AddWithValue("$folderId", prompt.FolderId.HasValue ? prompt.FolderId.Value : DBNull.Value);
            updateCommand.Parameters.AddWithValue("$type", promptType);
            updateCommand.Parameters.AddWithValue("$mode", promptMode);
            updateCommand.Parameters.AddWithValue("$labels", labels);
            updateCommand.Parameters.AddWithValue("$title", prompt.Title.Trim());
            updateCommand.Parameters.AddWithValue("$body", prompt.Body);
            updateCommand.Parameters.AddWithValue("$status", prompt.Status.ToString());
            updateCommand.Parameters.AddWithValue("$tags", prompt.Tags.Trim());
            updateCommand.Parameters.AddWithValue("$notes", prompt.Notes.Trim());
            updateCommand.Parameters.AddWithValue("$formattedNotes", prompt.FormattedNotes);
            updateCommand.Parameters.AddWithValue("$isFavorite", prompt.IsFavorite ? 1 : 0);
            updateCommand.Parameters.AddWithValue("$isArchived", prompt.IsArchived ? 1 : 0);
            updateCommand.Parameters.AddWithValue("$updatedAt", ToDatabaseDate(now));
            if (updateCommand.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("This prompt was changed or deleted since it was opened. Your edits have been kept. Copy them before refreshing, then review the latest record.");
        }

        if (recordChanged)
        {
            InsertPromptVersion(connection, transaction, prompt.Id, currentVersion + 1, prompt.FolderId, prompt.Title.Trim(), promptType, promptMode, labels, prompt.Body, prompt.Status.ToString(), prompt.Tags.Trim(), prompt.Notes.Trim(), prompt.FormattedNotes, normalizedChangeNote, now, prompt.IsArchived);
        }
    }

    public void SetFavorite(int promptId, bool isFavorite)
    {
        ExecutePromptUpdate("UPDATE Prompts SET IsFavorite = $value, UpdatedAt = $updatedAt, Revision = Revision + 1 WHERE Id = $id;", promptId, isFavorite ? 1 : 0);
    }

    public void ArchivePrompt(int promptId)
    {
        var prompt = GetPromptById(promptId) ?? throw new InvalidOperationException("The prompt no longer exists.");
        prompt.IsArchived = true;
        prompt.Status = PromptStatus.Retired;
        SavePrompt(prompt, "Archived prompt");
    }

    public void RestorePrompt(int promptId)
    {
        var prompt = GetPromptById(promptId) ?? throw new InvalidOperationException("The prompt no longer exists.");
        prompt.IsArchived = false;
        prompt.Status = PromptStatus.Draft;
        SavePrompt(prompt, "Restored archived prompt to Draft");
    }

    public void MovePrompts(IEnumerable<int> promptIds, int? folderId, string changeNote)
    {
        var prompts = promptIds.Distinct().Select(id => GetPromptById(id)
            ?? throw new InvalidOperationException("A selected prompt no longer exists.")).ToList();
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var prompt in prompts)
        {
            prompt.FolderId = folderId;
            SavePromptRecord(connection, transaction, prompt, changeNote);
        }
        transaction.Commit();
    }

    public PromptRow RestoreVersion(int promptId, int versionNumber, string changeNote, int? expectedRevision = null)
    {
        var prompt = GetPromptById(promptId) ?? throw new InvalidOperationException("The prompt no longer exists.");
        if (expectedRevision.HasValue && prompt.Revision != expectedRevision.Value)
            throw new InvalidOperationException("This prompt has changed. Refresh and review the latest record before restoring a version.");
        var version = GetPromptVersions(promptId).Single(item => item.VersionNumber == versionNumber);
        prompt.Body = version.Body;
        if (!string.IsNullOrEmpty(version.Title)) prompt.Title = version.Title;
        if (version.Type != null) prompt.Type = version.Type;
        if (version.Mode != null) prompt.Mode = version.Mode;
        if (version.Labels != null) prompt.Labels = version.Labels;
        if (version.Tags != null) prompt.Tags = version.Tags;
        if (version.Notes != null) prompt.Notes = version.Notes;
        if (version.FormattedNotes != null) prompt.FormattedNotes = version.FormattedNotes;
        if (version.Title != null)
            using (var connection = OpenConnection())
                prompt.FolderId = version.FolderId.HasValue && GetFolderPathMap(connection).ContainsKey(version.FolderId.Value) ? version.FolderId : null;
        prompt.Status = PromptStatus.Draft;
        prompt.IsArchived = false;
        return SavePrompt(prompt, "Restore v" + versionNumber + ": " + changeNote);
    }

    public void MarkUsed(int promptId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Prompts SET LastUsedAt = $lastUsedAt WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", promptId);
        command.Parameters.AddWithValue("$lastUsedAt", ToDatabaseDate(DateTime.Now));
        command.ExecuteNonQuery();
    }

    public void DeletePrompt(int promptId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Prompts WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", promptId);
        command.ExecuteNonQuery();
    }

    public PromptExportPackage CreateExportPackage(int? promptId)
    {
        using var connection = OpenConnection();
        var package = new PromptExportPackage();

        using (var folderCommand = connection.CreateCommand())
        {
            folderCommand.CommandText = "SELECT Id, ParentId, Name FROM Folders ORDER BY ParentId, Name;";
            using var reader = folderCommand.ExecuteReader();
            while (reader.Read())
            {
                package.Folders.Add(new FolderExportDto
                {
                    Id = reader.GetInt32(0),
                    ParentId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    Name = reader.GetString(2)
                });
            }
        }

        using (var promptCommand = connection.CreateCommand())
        {
            promptCommand.CommandText =
                "SELECT Id, FolderId, Type, Mode, Labels, Title, Body, Status, Tags, Notes, FormattedNotes, IsFavorite, IsArchived, CreatedAt, UpdatedAt, LastUsedAt " +
                "FROM Prompts " +
                (promptId.HasValue ? "WHERE Id = $id " : "") +
                "ORDER BY Title;";
            if (promptId.HasValue)
            {
                promptCommand.Parameters.AddWithValue("$id", promptId.Value);
            }

            using var reader = promptCommand.ExecuteReader();
            while (reader.Read())
            {
                var prompt = new PromptExportDto
                {
                    Id = reader.GetInt32(0),
                    FolderId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    Type = NormalizePromptType(reader.GetString(2)),
                    Mode = NormalizePromptMode(reader.GetString(3)),
                    Labels = reader.GetString(4),
                    Title = reader.GetString(5),
                    Body = reader.GetString(6),
                    Status = ParseStatus(reader.GetString(7)),
                    Tags = reader.GetString(8),
                    Notes = reader.GetString(9),
                    FormattedNotes = reader.GetString(10),
                    IsFavorite = reader.GetInt32(11) == 1,
                    IsArchived = reader.GetInt32(12) == 1,
                    CreatedAt = FromDatabaseDate(reader.GetString(13)) ?? DateTime.Now,
                    UpdatedAt = FromDatabaseDate(reader.GetString(14)) ?? DateTime.Now,
                    LastUsedAt = reader.IsDBNull(15) ? null : FromDatabaseDate(reader.GetString(15))
                };

                prompt.Versions.AddRange(GetPromptVersions(connection, prompt.Id));
                package.Prompts.Add(prompt);
            }
        }

        package.RecoveryHistory.AddRange(GetRecoveryHistory());
        return package;
    }

    public PromptExportPackage CreateScopedExport(IEnumerable<int>? promptIds)
    {
        var package = CreateExportPackage(null);
        if (promptIds != null)
        {
            package.RecoveryHistory.Clear();
            var ids = promptIds.ToHashSet();
            package.Prompts.RemoveAll(prompt => !ids.Contains(prompt.Id));
            var folderIds = package.Prompts.SelectMany(prompt => prompt.Versions.Select(version => version.FolderId).Append(prompt.FolderId))
                .Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
            var byId = package.Folders.ToDictionary(folder => folder.Id);
            foreach (var id in folderIds.ToArray())
            {
                var current = id;
                while (byId.TryGetValue(current, out var folder) && folder.ParentId.HasValue && folderIds.Add(folder.ParentId.Value))
                    current = folder.ParentId.Value;
            }
            package.Folders.RemoveAll(folder => !folderIds.Contains(folder.Id));
        }
        return package;
    }

    public int ImportPackage(PromptExportPackage package)
    {
        if (package.Format != "PromptManager.Export" || !Version.TryParse(package.FormatVersion, out var formatVersion)
            || formatVersion > new Version(1, 4) || formatVersion.Major != 1)
            throw new InvalidOperationException("This export format is unsupported. Use a supported Prompt Manager export.");
        if (package.Folders == null || package.Prompts == null
            || package.Folders.Any(folder => folder == null || folder.Id <= 0 || string.IsNullOrWhiteSpace(folder.Name))
            || package.Folders.Select(folder => folder.Id).Distinct().Count() != package.Folders.Count
            || package.Prompts.Any(prompt => prompt == null || prompt.Versions == null || prompt.Body == null || string.IsNullOrWhiteSpace(prompt.Title)
                || !Enum.IsDefined(prompt.Status) || prompt.Tags == null || prompt.Notes == null || prompt.FormattedNotes == null
                || prompt.Versions.Any(version => version == null || version.VersionNumber <= 0 || version.Body == null)
                || prompt.Versions.Select(version => version.VersionNumber).Distinct().Count() != prompt.Versions.Count))
            throw new InvalidOperationException("The export contains invalid prompt, folder, or version records.");
        if (package.RecoveryHistory == null || package.RecoveryHistory.Any(record => record == null || record.SnapshotJson == null || record.Reason == null))
            throw new InvalidOperationException("The export contains invalid recovery records.");
        foreach (var record in package.RecoveryHistory)
        {
            using var snapshot = JsonDocument.Parse(record.SnapshotJson);
            if (snapshot.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("A recovery snapshot must be a JSON object.");
        }
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var folderMap = new Dictionary<int, int>();
        var pendingFolders = package.Folders.ToList();
        while (pendingFolders.Count > 0)
        {
            var previousCount = pendingFolders.Count;
            foreach (var folder in pendingFolders.ToList())
            {
                if (folder.ParentId.HasValue && !folderMap.ContainsKey(folder.ParentId.Value))
                {
                    continue;
                }

                var parentId = folder.ParentId.HasValue ? folderMap[folder.ParentId.Value] : (int?)null;
                var actualId = EnsureFolder(connection, transaction, parentId, folder.Name);
                folderMap[folder.Id] = actualId;
                pendingFolders.Remove(folder);
            }
            if (pendingFolders.Count == previousCount)
                throw new InvalidOperationException("The export contains missing parent folders or a circular folder structure. Nothing was imported.");
        }

        var imported = 0;
        foreach (var record in package.RecoveryHistory) InsertRecoveryRecord(connection, transaction, record);
        foreach (var prompt in package.Prompts)
        {
            if (prompt.FolderId.HasValue && !folderMap.ContainsKey(prompt.FolderId.Value))
                throw new InvalidOperationException("An imported prompt refers to a missing folder. Nothing was imported.");
            var mappedFolderId = prompt.FolderId.HasValue && folderMap.TryGetValue(prompt.FolderId.Value, out var actualFolderId)
                ? actualFolderId
                : (int?)null;

            var newPromptId = InsertImportedPrompt(connection, transaction, prompt, mappedFolderId);
            IEnumerable<PromptVersionExportDto> versions = prompt.Versions.Count == 0
                ? new[] { new PromptVersionExportDto { VersionNumber = 1, FolderId = prompt.FolderId, Title = prompt.Title, Type = prompt.Type, Mode = prompt.Mode, Labels = prompt.Labels, Body = prompt.Body, Status = prompt.Status.ToString(), Tags = prompt.Tags, Notes = prompt.Notes, FormattedNotes = prompt.FormattedNotes, IsArchived = prompt.IsArchived, ChangeNote = "Imported", CreatedAt = DateTime.Now } }
                : prompt.Versions.OrderBy(version => version.VersionNumber);

            foreach (var version in versions)
            {
                int? historyFolderId = version.FolderId.HasValue && folderMap.TryGetValue(version.FolderId.Value, out var mappedHistoryFolder) ? mappedHistoryFolder : null;
                InsertPromptVersion(connection, transaction, newPromptId, version.VersionNumber, historyFolderId,
                    version.Title ?? "", version.Type ?? "", version.Mode ?? "", version.Labels ?? "", version.Body,
                    version.Status ?? "", version.Tags ?? "", version.Notes ?? "", version.FormattedNotes ?? "",
                    version.ChangeNote, version.CreatedAt, version.IsArchived ?? false,
                    version.FolderPath ?? (version.FolderId.HasValue && !historyFolderId.HasValue ? "(not recorded)" : null));
            }

            imported++;
        }

        transaction.Commit();
        return imported;
    }

    public void BackupTo(string destinationPath)
    {
        var destination = Path.GetFullPath(destinationPath);
        if (string.Equals(destination, Path.GetFullPath(DatabasePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choose a backup file different from the active library.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var stagedBackup = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            CopyDatabaseSnapshot(DatabasePath, stagedBackup);
            ValidateBackupDatabase(stagedBackup);
            File.Move(stagedBackup, destination, true);
        }
        finally { DeleteTemporaryDatabase(stagedBackup); }
    }

    public string? RestoreFrom(string sourcePath)
    {
        ValidateBackupDatabase(sourcePath);
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(DatabasePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choose a backup file different from the active library.");
        var stagedDatabase = DatabasePath + ".restore-" + Guid.NewGuid().ToString("N") + ".tmp";
        string? safetyBackupPath = null;
        try
        {
            CopyDatabaseSnapshot(sourcePath, stagedDatabase);
            var stagedRepository = new PromptRepository(stagedDatabase);
            stagedRepository.Initialize(false);
            stagedRepository.LoadFolderTree();
            stagedRepository.GetPrompts(PromptFilterKind.Retired, null, null);
            stagedRepository.GetPrompts(PromptFilterKind.All, null, null);
            ValidateBackupDatabase(stagedDatabase);
            if (File.Exists(DatabasePath))
            {
                safetyBackupPath = Path.Combine(Path.GetDirectoryName(DatabasePath)!,
                    "pre-restore-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..8] + ".db");
                BackupTo(safetyBackupPath);
                using (var connection = OpenConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA journal_mode=DELETE;";
                    if (!string.Equals(command.ExecuteScalar()?.ToString(), "delete", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("The library is in use. Close other applications using this database and retry the restore.");
                }
                File.Replace(stagedDatabase, DatabasePath, null);
            }
            else File.Move(stagedDatabase, DatabasePath);
            _isFtsAvailable = stagedRepository._isFtsAvailable;
            return safetyBackupPath;
        }
        finally { DeleteTemporaryDatabase(stagedDatabase); }
    }

    public static void ValidateBackupDatabase(string sourcePath, bool allowDetachedHistory = false)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The selected backup file was not found.", sourcePath);
        }

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        var applicationId = ReadPragma(connection, "application_id");
        if ((applicationId != 0 && applicationId != ApplicationId) || ReadPragma(connection, "user_version") > SchemaVersion)
            throw new InvalidOperationException("This is not a supported Prompt Manager database.");

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA integrity_check;";
            var result = command.ExecuteScalar() as string;
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The selected backup failed SQLite integrity_check: " + result);
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Prompts', 'Folders', 'PromptVersions');";
            var requiredTables = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (requiredTables < 3)
            {
                throw new InvalidOperationException("The selected database is not a complete Prompt Manager backup.");
            }
        }
        var columns = new Dictionary<string, string[]>
        {
            ["Folders"] = new[] { "Id", "ParentId", "Name", "SortOrder", "CreatedAt", "UpdatedAt" },
            ["Prompts"] = new[] { "Id", "FolderId", "Title", "Body", "Status", "Tags", "Notes", "IsFavorite", "IsArchived", "CreatedAt", "UpdatedAt", "LastUsedAt" },
            ["PromptVersions"] = new[] { "Id", "PromptId", "VersionNumber", "Body", "ChangeNote", "CreatedAt" }
        };
        foreach (var table in columns)
            foreach (var column in table.Value)
                if (!ColumnExists(connection, table.Key, column))
                    throw new InvalidOperationException("The backup has an incompatible schema: " + table.Key + "." + column);
        using var foreignKeys = connection.CreateCommand();
        foreignKeys.CommandText = "PRAGMA foreign_key_check;";
        using (var foreignKeyReader = foreignKeys.ExecuteReader())
            while (foreignKeyReader.Read())
                if (!(allowDetachedHistory && foreignKeyReader.GetString(0) == "PromptVersions" && foreignKeyReader.GetString(2) == "Prompts"))
                    throw new InvalidOperationException("The backup contains broken record relationships.");
        using var records = connection.CreateCommand();
        records.CommandText = """
            SELECT (SELECT COUNT(*) FROM Prompts WHERE Status NOT IN ('Draft','InReview','Approved','Retired') OR IsArchived NOT IN (0,1) OR IsFavorite NOT IN (0,1)
                OR (FolderId IS NOT NULL AND FolderId NOT IN (SELECT Id FROM Folders)))
                + (SELECT COUNT(*) FROM Folders WHERE ParentId IS NOT NULL AND ParentId NOT IN (SELECT Id FROM Folders))
                + (SELECT COUNT(*) FROM PromptVersions WHERE VersionNumber < 1)
                + (SELECT COUNT(*) FROM (SELECT PromptId, VersionNumber FROM PromptVersions GROUP BY PromptId,VersionNumber HAVING COUNT(*) > 1));
            """;
        if (Convert.ToInt64(records.ExecuteScalar()) != 0) throw new InvalidOperationException("The backup contains invalid records or relationships.");
        if (!allowDetachedHistory)
        {
            records.CommandText = "SELECT COUNT(*) FROM PromptVersions WHERE PromptId NOT IN (SELECT Id FROM Prompts);";
            if (Convert.ToInt64(records.ExecuteScalar()) != 0) throw new InvalidOperationException("The backup contains detached version history.");
        }
        GetFolderPathMap(connection);
    }

    private static int ReadPragma(SqliteConnection connection, string name)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA " + name + ";";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void CopyDatabaseSnapshot(string sourcePath, string destinationPath)
    {
        using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destinationPath, Pooling = false }.ToString());
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }

    private static void DeleteTemporaryDatabase(string path)
    {
        foreach (var temporaryFile in new[] { path, path + "-wal", path + "-shm" })
            if (File.Exists(temporaryFile)) File.Delete(temporaryFile);
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Pooling = false
        }.ToString());
        connection.Open();
        ExecuteNonQuery(connection, "PRAGMA foreign_keys = ON;");
        return connection;
    }

    private static void CreateSchema(SqliteConnection connection)
    {
        CreateRecoveryTable(connection);
        ExecuteNonQuery(connection,
            """
            CREATE TABLE IF NOT EXISTS Folders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ParentId INTEGER NULL REFERENCES Folders(Id) ON DELETE CASCADE,
                Name TEXT NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Prompts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FolderId INTEGER NULL REFERENCES Folders(Id) ON DELETE SET NULL,
                Type TEXT NOT NULL DEFAULT 'Prompt',
                Mode TEXT NOT NULL DEFAULT 'Text',
                Labels TEXT NOT NULL DEFAULT '',
                Title TEXT NOT NULL,
                Body TEXT NOT NULL,
                Status TEXT NOT NULL,
                Tags TEXT NOT NULL DEFAULT '',
                Notes TEXT NOT NULL DEFAULT '',
                FormattedNotes TEXT NOT NULL DEFAULT '',
                IsFavorite INTEGER NOT NULL DEFAULT 0,
                IsArchived INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                LastUsedAt TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS PromptTypes (
                Name TEXT PRIMARY KEY COLLATE NOCASE
            );

            CREATE TABLE IF NOT EXISTS PromptLabels (
                Name TEXT PRIMARY KEY COLLATE NOCASE
            );

            CREATE TABLE IF NOT EXISTS PromptVersions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PromptId INTEGER NOT NULL REFERENCES Prompts(Id) ON DELETE CASCADE,
                VersionNumber INTEGER NOT NULL,
                Body TEXT NOT NULL,
                ChangeNote TEXT NOT NULL DEFAULT '',
                FolderId INTEGER NULL,
                Type TEXT NOT NULL DEFAULT 'Prompt',
                Mode TEXT NOT NULL DEFAULT 'Text',
                Labels TEXT NOT NULL DEFAULT '',
                Title TEXT NOT NULL DEFAULT '',
                Status TEXT NOT NULL DEFAULT 'Draft',
                Tags TEXT NOT NULL DEFAULT '',
                Notes TEXT NOT NULL DEFAULT '',
                FormattedNotes TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Folders_ParentId_Name ON Folders (ParentId, Name);
            CREATE INDEX IF NOT EXISTS IX_Prompts_Folder_Status ON Prompts (FolderId, Status);
            CREATE INDEX IF NOT EXISTS IX_Prompts_UpdatedAt ON Prompts (UpdatedAt);
            """);
    }

    private static void MigrateSchema(SqliteConnection connection)
    {
        AddColumnIfMissing(connection, "Prompts", "Type", "TEXT NOT NULL DEFAULT 'Prompt'");
        AddColumnIfMissing(connection, "Prompts", "Mode", "TEXT NOT NULL DEFAULT 'Text'");
        AddColumnIfMissing(connection, "Prompts", "Labels", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "Prompts", "FormattedNotes", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "Prompts", "Revision", "INTEGER NOT NULL DEFAULT 0");
        ExecuteNonQuery(connection, "UPDATE Prompts SET Type = 'Prompt' WHERE Type IS NULL OR TRIM(Type) = '';");
        ExecuteNonQuery(connection, "UPDATE Prompts SET Mode = 'Text' WHERE Mode IS NULL OR TRIM(Mode) = '';");
        ExecuteNonQuery(connection, "UPDATE Prompts SET Labels = '' WHERE Labels IS NULL;");
        ExecuteNonQuery(connection, "UPDATE Prompts SET FormattedNotes = '' WHERE FormattedNotes IS NULL;");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_Prompts_Type ON Prompts (Type);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_Prompts_Mode ON Prompts (Mode);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_Prompts_Labels ON Prompts (Labels);");
        AddColumnIfMissing(connection, "PromptVersions", "FolderId", "INTEGER NULL");
        AddColumnIfMissing(connection, "PromptVersions", "Type", "TEXT NOT NULL DEFAULT 'Prompt'");
        AddColumnIfMissing(connection, "PromptVersions", "Mode", "TEXT NOT NULL DEFAULT 'Text'");
        AddColumnIfMissing(connection, "PromptVersions", "Labels", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "PromptVersions", "Title", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "PromptVersions", "Status", "TEXT NOT NULL DEFAULT 'Draft'");
        AddColumnIfMissing(connection, "PromptVersions", "Tags", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "PromptVersions", "Notes", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "PromptVersions", "FormattedNotes", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "PromptVersions", "FolderPath", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "PromptVersions", "IsArchived", "INTEGER NOT NULL DEFAULT 0");
    }

    private static void AddColumnIfMissing(SqliteConnection connection, string tableName, string columnName, string definition)
    {
        if (ColumnExists(connection, tableName, columnName))
        {
            return;
        }

        ExecuteNonQuery(connection, "ALTER TABLE " + tableName + " ADD COLUMN " + columnName + " " + definition + ";");
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(" + tableName + ");";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryEnsureFullTextSearch(SqliteConnection connection)
    {
        try
        {
            using var triggerCheck = connection.CreateCommand();
            triggerCheck.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='trigger' AND name IN ('Prompts_ai','Prompts_ad','Prompts_au');";
            if (ColumnExists(connection, "PromptSearch", "Mode") && ColumnExists(connection, "PromptSearch", "Labels")
                && Convert.ToInt32(triggerCheck.ExecuteScalar()) == 3) return true;
            ExecuteNonQuery(connection,
                """
                DROP TRIGGER IF EXISTS Prompts_ai;
                DROP TRIGGER IF EXISTS Prompts_ad;
                DROP TRIGGER IF EXISTS Prompts_au;
                DROP TABLE IF EXISTS PromptSearch;

                CREATE VIRTUAL TABLE IF NOT EXISTS PromptSearch USING fts5(
                    Type,
                    Mode,
                    Labels,
                    Title,
                    Body,
                    Tags,
                    Notes,
                    content='Prompts',
                    content_rowid='Id'
                );

                CREATE TRIGGER IF NOT EXISTS Prompts_ai AFTER INSERT ON Prompts BEGIN
                    INSERT INTO PromptSearch(rowid, Type, Mode, Labels, Title, Body, Tags, Notes)
                    VALUES (new.Id, new.Type, new.Mode, new.Labels, new.Title, new.Body, new.Tags, new.Notes);
                END;

                CREATE TRIGGER IF NOT EXISTS Prompts_ad AFTER DELETE ON Prompts BEGIN
                    INSERT INTO PromptSearch(PromptSearch, rowid, Type, Mode, Labels, Title, Body, Tags, Notes)
                    VALUES ('delete', old.Id, old.Type, old.Mode, old.Labels, old.Title, old.Body, old.Tags, old.Notes);
                END;

                CREATE TRIGGER IF NOT EXISTS Prompts_au AFTER UPDATE ON Prompts BEGIN
                    INSERT INTO PromptSearch(PromptSearch, rowid, Type, Mode, Labels, Title, Body, Tags, Notes)
                    VALUES ('delete', old.Id, old.Type, old.Mode, old.Labels, old.Title, old.Body, old.Tags, old.Notes);
                    INSERT INTO PromptSearch(rowid, Type, Mode, Labels, Title, Body, Tags, Notes)
                    VALUES (new.Id, new.Type, new.Mode, new.Labels, new.Title, new.Body, new.Tags, new.Notes);
                END;
                """);
            ExecuteNonQuery(connection, "INSERT INTO PromptSearch(PromptSearch) VALUES('rebuild');");
            return true;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    private static void SeedIfEmpty(SqliteConnection connection)
    {
        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM Prompts;";
        var existing = Convert.ToInt32(countCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (existing > 0)
        {
            return;
        }

        var validation = InsertSeedFolder(connection, null, "Validation");
        var csv = InsertSeedFolder(connection, validation, "CSV");
        var equipment = InsertSeedFolder(connection, validation, "Equipment Qualification");
        var software = InsertSeedFolder(connection, null, "Software / CSV");
        InsertSeedFolder(connection, null, "Business Dev");

        InsertSeedPrompt(connection, csv, "CSV Protocol Builder", PromptStatus.Draft, "CSV; protocol; validation",
            "Create a validation protocol outline for a configurable GxP computerized system.");
        InsertSeedPrompt(connection, csv, "Part 11 Test Matrix", PromptStatus.Draft, "Part 11; test matrix; compliance",
            "Generate a test matrix covering electronic records, electronic signatures, audit trail, access control, and record retention requirements.");
        InsertSeedPrompt(connection, equipment, "IQ/OQ Generator", PromptStatus.InReview, "IQ; OQ; equipment",
            "Build an IQ/OQ protocol skeleton for qualified equipment, including prerequisites, test scripts, acceptance criteria, and deviation handling.");
        InsertSeedPrompt(connection, software, "Change Control Impact Review", PromptStatus.Draft, "change control; impact; software",
            "Assess the validation impact of a proposed software change and identify required regression testing.");
    }

    private static void SeedPromptTypes(SqliteConnection connection)
    {
        EnsurePromptType(connection, null, "Prompt");
        ExecuteNonQuery(connection,
            """
            INSERT OR IGNORE INTO PromptTypes (Name)
            SELECT DISTINCT Type
            FROM Prompts
            WHERE Type IS NOT NULL AND TRIM(Type) <> '';
            """);
    }

    private static void SeedPromptLabels(SqliteConnection connection)
    {
        foreach (var label in new[] { "Production", "Approved", "Training", "Deprecated" })
        {
            EnsurePromptLabel(connection, null, label);
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Labels FROM Prompts WHERE Labels IS NOT NULL AND TRIM(Labels) <> '';";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            EnsurePromptLabels(connection, null, reader.GetString(0));
        }
    }

    private static int InsertSeedFolder(SqliteConnection connection, int? parentId, string name)
    {
        var now = DateTime.Now;
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO Folders (ParentId, Name, SortOrder, CreatedAt, UpdatedAt) " +
            "VALUES ($parentId, $name, 0, $createdAt, $updatedAt); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$parentId", parentId.HasValue ? parentId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$createdAt", ToDatabaseDate(now));
        command.Parameters.AddWithValue("$updatedAt", ToDatabaseDate(now));
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void InsertSeedPrompt(SqliteConnection connection, int folderId, string title, PromptStatus status, string tags, string body)
    {
        var now = DateTime.Now;
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO Prompts (FolderId, Type, Mode, Labels, Title, Body, Status, Tags, Notes, FormattedNotes, IsFavorite, IsArchived, CreatedAt, UpdatedAt, LastUsedAt) " +
            "VALUES ($folderId, 'Prompt', 'Text', '', $title, $body, $status, $tags, '', '', 0, 0, $createdAt, $updatedAt, NULL); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$folderId", folderId);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$body", body);
        command.Parameters.AddWithValue("$status", status.ToString());
        command.Parameters.AddWithValue("$tags", tags);
        command.Parameters.AddWithValue("$createdAt", ToDatabaseDate(now));
        command.Parameters.AddWithValue("$updatedAt", ToDatabaseDate(now));
        var promptId = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        InsertPromptVersion(connection, null, promptId, 1, folderId, title, "Prompt", "Text", "", body, status.ToString(), tags, "", "", "Seed prompt", now);
    }

    private static IReadOnlyList<PromptRow> ReadPrompts(SqliteCommand command, IReadOnlyDictionary<int, string> folderPaths)
    {
        var prompts = new List<PromptRow>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            int? folderId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
            var folderPath = "Unfiled";
            if (folderId.HasValue && folderPaths.TryGetValue(folderId.Value, out var resolvedPath))
            {
                folderPath = resolvedPath;
            }

            prompts.Add(new PromptRow
            {
                Id = reader.GetInt32(0),
                FolderId = folderId,
                Type = NormalizePromptType(reader.GetString(2)),
                Mode = NormalizePromptMode(reader.GetString(3)),
                Labels = reader.GetString(4),
                Title = reader.GetString(5),
                Body = reader.GetString(6),
                Status = ParseStatus(reader.GetString(7)),
                Tags = reader.GetString(8),
                Notes = reader.GetString(9),
                FormattedNotes = reader.GetString(10),
                IsFavorite = reader.GetInt32(11) == 1,
                IsArchived = reader.GetInt32(12) == 1,
                CreatedAt = FromDatabaseDate(reader.GetString(13)) ?? DateTime.Now,
                UpdatedAt = FromDatabaseDate(reader.GetString(14)) ?? DateTime.Now,
                LastUsedAt = reader.IsDBNull(15) ? null : FromDatabaseDate(reader.GetString(15)),
                Version = reader.GetInt32(16),
                Revision = reader.GetInt32(17),
                FolderPath = folderPath
            });
        }

        return prompts;
    }

    private static Dictionary<int, string> GetFolderPathMap(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id, ParentId, Name FROM Folders;";

        var folders = new Dictionary<int, (int? ParentId, string Name)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                folders[reader.GetInt32(0)] = (reader.IsDBNull(1) ? null : reader.GetInt32(1), reader.GetString(2));
            }
        }

        var cache = new Dictionary<int, string>();
        var resolving = new HashSet<int>();
        string Resolve(int id)
        {
            if (cache.TryGetValue(id, out var path))
            {
                return path;
            }

            if (!folders.TryGetValue(id, out var folder))
            {
                return "Unfiled";
            }
            if (!resolving.Add(id)) throw new InvalidOperationException("The library contains a circular folder structure.");
            if (resolving.Count > 256) throw new InvalidOperationException("The folder structure exceeds the supported depth of 256 levels.");
            path = folder.ParentId.HasValue ? Resolve(folder.ParentId.Value) + " / " + folder.Name : folder.Name;
            resolving.Remove(id);
            cache[id] = path;
            return path;
        }

        foreach (var id in folders.Keys)
        {
            Resolve(id);
        }

        return cache;
    }

    private static List<int> GetDescendantFolderIds(SqliteConnection connection, int folderId, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id, ParentId FROM Folders;";
        var childrenByParent = new Dictionary<int, List<int>>();

        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var id = reader.GetInt32(0);
                if (reader.IsDBNull(1))
                {
                    continue;
                }

                var parentId = reader.GetInt32(1);
                if (!childrenByParent.TryGetValue(parentId, out var children))
                {
                    children = new List<int>();
                    childrenByParent[parentId] = children;
                }

                children.Add(id);
            }
        }

        var result = new List<int>();
        var seen = new HashSet<int>();
        void AddWithChildren(int id)
        {
            if (!seen.Add(id)) throw new InvalidOperationException("The library contains a circular folder structure.");
            result.Add(id);
            if (!childrenByParent.TryGetValue(id, out var children))
            {
                return;
            }

            foreach (var child in children)
            {
                AddWithChildren(child);
            }
        }

        AddWithChildren(folderId);
        return result;
    }

    private static void ApplyFilter(ICollection<string> where, PromptFilterKind filter)
    {
        switch (filter)
        {
            case PromptFilterKind.Favorites:
                where.Add("p.IsFavorite = 1");
                where.Add("p.IsArchived = 0");
                break;
            case PromptFilterKind.RecentlyUsed:
                where.Add("p.LastUsedAt IS NOT NULL");
                where.Add("p.IsArchived = 0");
                break;
            case PromptFilterKind.Drafts:
                where.Add("p.Status = 'Draft'");
                where.Add("p.IsArchived = 0");
                break;
            case PromptFilterKind.Approved:
                where.Add("p.Status = 'Approved'");
                where.Add("p.IsArchived = 0");
                break;
            case PromptFilterKind.InReview:
                where.Add("p.Status = 'InReview'");
                where.Add("p.IsArchived = 0");
                break;
            case PromptFilterKind.Retired:
                where.Add("(p.Status = 'Retired' OR p.IsArchived = 1)");
                break;
            default:
                where.Add("p.IsArchived = 0");
                break;
        }
    }

    private static int CountPrompts(SqliteConnection connection, string where)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Prompts WHERE " + where + ";";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public IReadOnlyList<PromptVersionExportDto> GetPromptVersions(int promptId)
    {
        using var connection = OpenConnection();
        return GetPromptVersions(connection, promptId);
    }

    private static IReadOnlyList<PromptVersionExportDto> GetPromptVersions(SqliteConnection connection, int promptId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT VersionNumber, Body, ChangeNote, CreatedAt, FolderId, Type, Mode, Labels, Title, Status, Tags, Notes, FormattedNotes, FolderPath, IsArchived FROM PromptVersions WHERE PromptId = $promptId ORDER BY VersionNumber;";
        command.Parameters.AddWithValue("$promptId", promptId);

        var versions = new List<PromptVersionExportDto>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var metadataKnown = !string.IsNullOrEmpty(reader.GetString(8));
            versions.Add(new PromptVersionExportDto
            {
                VersionNumber = reader.GetInt32(0),
                Body = reader.GetString(1),
                ChangeNote = reader.GetString(2),
                CreatedAt = FromDatabaseDate(reader.GetString(3)) ?? DateTime.Now,
                FolderId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                Type = metadataKnown ? reader.GetString(5) : null,
                Mode = metadataKnown ? reader.GetString(6) : null,
                Labels = metadataKnown ? reader.GetString(7) : null,
                Title = metadataKnown ? reader.GetString(8) : null,
                Status = metadataKnown ? reader.GetString(9) : null,
                Tags = metadataKnown ? reader.GetString(10) : null,
                Notes = metadataKnown ? reader.GetString(11) : null,
                FormattedNotes = metadataKnown ? reader.GetString(12) : null,
                FolderPath = metadataKnown ? reader.GetString(13) : null,
                IsArchived = metadataKnown ? reader.GetInt32(14) == 1 : null
            });
        }

        return versions;
    }

    private static int EnsureFolder(SqliteConnection connection, SqliteTransaction transaction, int? parentId, string name)
    {
        using (var readCommand = connection.CreateCommand())
        {
            readCommand.Transaction = transaction;
            readCommand.CommandText =
                "SELECT Id FROM Folders WHERE Name = $name AND " +
                "((ParentId IS NULL AND $parentId IS NULL) OR ParentId = $parentId) LIMIT 1;";
            readCommand.Parameters.AddWithValue("$name", name);
            readCommand.Parameters.AddWithValue("$parentId", parentId.HasValue ? parentId.Value : DBNull.Value);
            var existing = readCommand.ExecuteScalar();
            if (existing is not null && existing != DBNull.Value)
            {
                return Convert.ToInt32(existing, CultureInfo.InvariantCulture);
            }
        }

        var now = DateTime.Now;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "INSERT INTO Folders (ParentId, Name, SortOrder, CreatedAt, UpdatedAt) " +
            "VALUES ($parentId, $name, 0, $createdAt, $updatedAt); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$parentId", parentId.HasValue ? parentId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$createdAt", ToDatabaseDate(now));
        command.Parameters.AddWithValue("$updatedAt", ToDatabaseDate(now));
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static int InsertImportedPrompt(SqliteConnection connection, SqliteTransaction transaction, PromptExportDto prompt, int? mappedFolderId)
    {
        var promptType = NormalizePromptType(prompt.Type);
        EnsurePromptType(connection, transaction, promptType);
        var promptMode = NormalizePromptMode(prompt.Mode);
        var labels = NormalizeLabels(prompt.Labels);
        EnsurePromptLabels(connection, transaction, labels);

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "INSERT INTO Prompts (FolderId, Type, Mode, Labels, Title, Body, Status, Tags, Notes, FormattedNotes, IsFavorite, IsArchived, CreatedAt, UpdatedAt, LastUsedAt) " +
            "VALUES ($folderId, $type, $mode, $labels, $title, $body, $status, $tags, $notes, $formattedNotes, $isFavorite, $isArchived, $createdAt, $updatedAt, $lastUsedAt); " +
            "SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$folderId", mappedFolderId.HasValue ? mappedFolderId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$type", promptType);
        command.Parameters.AddWithValue("$mode", promptMode);
        command.Parameters.AddWithValue("$labels", labels);
        command.Parameters.AddWithValue("$title", prompt.Title);
        command.Parameters.AddWithValue("$body", prompt.Body);
        command.Parameters.AddWithValue("$status", prompt.Status.ToString());
        command.Parameters.AddWithValue("$tags", prompt.Tags);
        command.Parameters.AddWithValue("$notes", prompt.Notes);
        command.Parameters.AddWithValue("$formattedNotes", prompt.FormattedNotes);
        command.Parameters.AddWithValue("$isFavorite", prompt.IsFavorite ? 1 : 0);
        command.Parameters.AddWithValue("$isArchived", prompt.IsArchived ? 1 : 0);
        command.Parameters.AddWithValue("$createdAt", ToDatabaseDate(prompt.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", ToDatabaseDate(prompt.UpdatedAt));
        command.Parameters.AddWithValue("$lastUsedAt", prompt.LastUsedAt.HasValue ? ToDatabaseDate(prompt.LastUsedAt.Value) : DBNull.Value);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void InsertPromptVersion(SqliteConnection connection, SqliteTransaction? transaction, int promptId, int versionNumber, string body, string changeNote, DateTime createdAt)
    {
        InsertPromptVersion(connection, transaction, promptId, versionNumber, null, "", "Prompt", "Text", "", body, "Draft", "", "", "", changeNote, createdAt);
    }

    private static void InsertPromptVersion(SqliteConnection connection, SqliteTransaction? transaction, int promptId, int versionNumber, int? folderId, string title, string type, string mode, string labels, string body, string status, string tags, string notes, string formattedNotes, string changeNote, DateTime createdAt, bool isArchived = false, string? folderPath = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "INSERT INTO PromptVersions (PromptId, VersionNumber, FolderId, Type, Mode, Labels, Title, Body, Status, Tags, Notes, FormattedNotes, ChangeNote, CreatedAt, FolderPath, IsArchived) " +
            "VALUES ($promptId, $versionNumber, $folderId, $type, $mode, $labels, $title, $body, $status, $tags, $notes, $formattedNotes, $changeNote, $createdAt, $folderPath, $isArchived);";
        command.Parameters.AddWithValue("$folderPath", folderPath ?? (folderId.HasValue && GetFolderPathMap(connection, transaction).TryGetValue(folderId.Value, out var path) ? path : "Unfiled"));
        command.Parameters.AddWithValue("$isArchived", isArchived ? 1 : 0);
        command.Parameters.AddWithValue("$promptId", promptId);
        command.Parameters.AddWithValue("$versionNumber", versionNumber);
        command.Parameters.AddWithValue("$folderId", folderId.HasValue ? folderId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$type", NormalizePromptType(type));
        command.Parameters.AddWithValue("$mode", NormalizePromptMode(mode));
        command.Parameters.AddWithValue("$labels", NormalizeLabels(labels));
        command.Parameters.AddWithValue("$title", title.Trim());
        command.Parameters.AddWithValue("$body", body);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$tags", tags);
        command.Parameters.AddWithValue("$notes", notes);
        command.Parameters.AddWithValue("$formattedNotes", formattedNotes);
        command.Parameters.AddWithValue("$changeNote", changeNote);
        command.Parameters.AddWithValue("$createdAt", ToDatabaseDate(createdAt));
        command.ExecuteNonQuery();
    }

    private static int GetCurrentVersion(SqliteConnection connection, SqliteTransaction? transaction, int promptId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COALESCE(MAX(VersionNumber), 0) FROM PromptVersions WHERE PromptId = $id;";
        command.Parameters.AddWithValue("$id", promptId);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static bool HasControlledRecordChanges(SqliteConnection connection, SqliteTransaction? transaction, PromptRow prompt, string promptType, string promptMode, string labels)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT FolderId, Type, Mode, Labels, Title, Body, Status, Tags, Notes, FormattedNotes, IsArchived FROM Prompts WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", prompt.Id);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return true;
        }

        int? folderId = reader.IsDBNull(0) ? null : reader.GetInt32(0);
        return folderId != prompt.FolderId
            || !Same(reader.GetString(1), promptType)
            || !Same(reader.GetString(2), promptMode)
            || !Same(reader.GetString(3), labels)
            || !Same(reader.GetString(4), prompt.Title.Trim())
            || !Same(reader.GetString(5), prompt.Body)
            || !Same(reader.GetString(6), prompt.Status.ToString())
            || !Same(reader.GetString(7), prompt.Tags.Trim())
            || !Same(reader.GetString(8), prompt.Notes.Trim())
            || !Same(reader.GetString(9), prompt.FormattedNotes)
            || (reader.GetInt32(10) == 1) != prompt.IsArchived;
    }

    private static bool Same(string left, string right)
    {
        return string.Equals(left, right, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> FindPromptTitles(SqliteConnection connection, int promptId, string predicate, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT Title FROM Prompts WHERE Id <> $id AND IsArchived = 0 AND " + predicate + " ORDER BY UpdatedAt DESC LIMIT 3;";
        command.Parameters.AddWithValue("$id", promptId);
        command.Parameters.AddWithValue("$value", value);

        var titles = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            titles.Add(reader.GetString(0));
        }

        return titles;
    }

    private static void EnsurePromptType(SqliteConnection connection, SqliteTransaction? transaction, string? type)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT OR IGNORE INTO PromptTypes (Name) VALUES ($name);";
        command.Parameters.AddWithValue("$name", NormalizePromptType(type));
        command.ExecuteNonQuery();
    }

    private static void EnsurePromptLabels(SqliteConnection connection, SqliteTransaction? transaction, string labels)
    {
        foreach (var label in SplitLabels(labels))
        {
            EnsurePromptLabel(connection, transaction, label);
        }
    }

    private static void EnsurePromptLabel(SqliteConnection connection, SqliteTransaction? transaction, string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT OR IGNORE INTO PromptLabels (Name) VALUES ($name);";
        command.Parameters.AddWithValue("$name", label.Trim());
        command.ExecuteNonQuery();
    }

    private static string NormalizePromptType(string? type)
    {
        return string.IsNullOrWhiteSpace(type) ? "Prompt" : type.Trim();
    }

    private static string NormalizePromptMode(string? mode)
    {
        return string.IsNullOrWhiteSpace(mode) ? "Text" : mode.Trim();
    }

    private static string NormalizeLabels(string? labels)
    {
        return string.Join("; ", SplitLabels(labels ?? ""));
    }

    private static IReadOnlyList<string> SplitLabels(string labels)
    {
        return labels
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private void ExecutePromptUpdate(string sql, int promptId, int value)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", promptId);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$updatedAt", ToDatabaseDate(DateTime.Now));
        command.ExecuteNonQuery();
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static PromptStatus ParseStatus(string value)
    {
        return Enum.TryParse<PromptStatus>(value, out var status) ? status : PromptStatus.Draft;
    }

    private static string BuildFtsQuery(string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return "";
        }

        var terms = Regex.Matches(searchText, "[A-Za-z0-9_]+")
            .Select(match => match.Value)
            .Where(term => term.Length > 0)
            .Select(term => term + "*")
            .ToList();

        return terms.Count == 0 ? "" : string.Join(" AND ", terms);
    }

    private static string ToDatabaseDate(DateTime value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    private static DateTime? FromDatabaseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result)
            ? result
            : null;
    }
}





