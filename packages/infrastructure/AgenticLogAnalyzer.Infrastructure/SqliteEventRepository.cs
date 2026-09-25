using System.Globalization;
using System.Text.Json;
using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Domain.Logs;
using Microsoft.Data.Sqlite;

namespace AgenticLogAnalyzer.Infrastructure;

/// <summary>Stores canonical events in a local SQLite database for development deployments.</summary>
public sealed class SqliteEventRepository : IEventRepository, IDisposable
{
    private readonly string _connectionString;
    private readonly string[] _legacyJsonLinesPaths;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private volatile bool _initialized;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SqliteEventRepository(string path, IEnumerable<string>? legacyJsonLinesPaths = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _legacyJsonLinesPaths = legacyJsonLinesPaths?
            .Select(Path.GetFullPath)
            .Where(candidate => !string.Equals(candidate, fullPath, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
            DefaultTimeout = 30
        }.ToString();
    }

    public void Dispose()
    {
        _initializationGate.Dispose();
        GC.SuppressFinalize(this);
    }

    public async Task SaveAsync(CanonicalEvent canonicalEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(canonicalEvent);
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO CanonicalEvents (
                Id, TimestampUtcTicks, OffsetTicks, SourceType, SourceName,
                Category, Action, Result, UserName, Device, SourceIp, RawContent, Attributes)
            VALUES (
                $id, $timestampUtcTicks, $offsetTicks, $sourceType, $sourceName,
                $category, $action, $result, $userName, $device, $sourceIp, $rawContent, $attributes)
            ON CONFLICT(Id) DO UPDATE SET
                TimestampUtcTicks = excluded.TimestampUtcTicks,
                OffsetTicks = excluded.OffsetTicks,
                SourceType = excluded.SourceType,
                SourceName = excluded.SourceName,
                Category = excluded.Category,
                Action = excluded.Action,
                Result = excluded.Result,
                UserName = excluded.UserName,
                Device = excluded.Device,
                SourceIp = excluded.SourceIp,
                RawContent = excluded.RawContent,
                Attributes = excluded.Attributes;
            """;
        AddEventParameters(command, canonicalEvent);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CanonicalEvent>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, TimestampUtcTicks, OffsetTicks, SourceType, SourceName,
                   Category, Action, Result, UserName, Device, SourceIp, RawContent, Attributes
            FROM CanonicalEvents
            WHERE $query = ''
               OR RawContent COLLATE NOCASE LIKE $pattern ESCAPE '\'
               OR SourceType COLLATE NOCASE LIKE $pattern ESCAPE '\'
               OR SourceName COLLATE NOCASE LIKE $pattern ESCAPE '\'
               OR Category COLLATE NOCASE LIKE $pattern ESCAPE '\'
               OR Action COLLATE NOCASE LIKE $pattern ESCAPE '\'
               OR COALESCE(Result, '') COLLATE NOCASE LIKE $pattern ESCAPE '\'
               OR COALESCE(UserName, '') COLLATE NOCASE LIKE $pattern ESCAPE '\'
               OR COALESCE(Device, '') COLLATE NOCASE LIKE $pattern ESCAPE '\'
               OR COALESCE(SourceIp, '') COLLATE NOCASE LIKE $pattern ESCAPE '\'
               OR COALESCE(Attributes, '') COLLATE NOCASE LIKE $pattern ESCAPE '\'
            ORDER BY TimestampUtcTicks DESC, Id;
            """;

        var normalizedQuery = string.IsNullOrWhiteSpace(query) ? string.Empty : query;
        var escapedQuery = normalizedQuery
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        command.Parameters.AddWithValue("$query", normalizedQuery);
        command.Parameters.AddWithValue("$pattern", $"%{escapedQuery}%");

        var events = new List<CanonicalEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var utcTicks = reader.GetInt64(1);
            var offsetTicks = reader.GetInt64(2);
            var offset = TimeSpan.FromTicks(offsetTicks);
            var localDateTime = new DateTime(checked(utcTicks + offsetTicks), DateTimeKind.Unspecified);
            events.Add(new CanonicalEvent(
                Guid.Parse(reader.GetString(0)),
                new DateTimeOffset(localDateTime, offset),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.GetString(11),
                reader.IsDBNull(12) ? null : DeserializeAttributes(reader.GetString(12))));
        }

        return events;
    }

    public async Task<int> DeleteAsync(string? sourceName, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sourceName is null
            ? "DELETE FROM CanonicalEvents;"
            : "DELETE FROM CanonicalEvents WHERE SourceName = $sourceName;";
        if (sourceName is not null)
        {
            command.Parameters.AddWithValue("$sourceName", sourceName);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationGate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode = WAL;
                CREATE TABLE IF NOT EXISTS CanonicalEvents (
                    Id TEXT NOT NULL PRIMARY KEY,
                    TimestampUtcTicks INTEGER NOT NULL,
                    OffsetTicks INTEGER NOT NULL,
                    SourceType TEXT NOT NULL,
                    SourceName TEXT NOT NULL,
                    Category TEXT NOT NULL,
                    Action TEXT NOT NULL,
                    Result TEXT NULL,
                    UserName TEXT NULL,
                    Device TEXT NULL,
                    SourceIp TEXT NULL,
                    RawContent TEXT NOT NULL,
                    Attributes TEXT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_CanonicalEvents_TimestampUtcTicks
                    ON CanonicalEvents (TimestampUtcTicks DESC);
                CREATE TABLE IF NOT EXISTS StorageMetadata (
                    Key TEXT NOT NULL PRIMARY KEY,
                    Value TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            await AddAttributesColumnIfMissingAsync(connection, cancellationToken);
            await ImportLegacyJsonLinesIfEmptyAsync(connection, cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    // Databases created before attributes were stored lack the column; their events keep null attributes.
    private static async Task AddAttributesColumnIfMissingAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('CanonicalEvents') WHERE name = 'Attributes';";
        if ((long)(await check.ExecuteScalarAsync(cancellationToken) ?? 0L) > 0)
        {
            return;
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE CanonicalEvents ADD COLUMN Attributes TEXT NULL;";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Dictionary<string, string>? DeserializeAttributes(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);

    private async Task ImportLegacyJsonLinesIfEmptyAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (_legacyJsonLinesPaths.Length == 0)
        {
            return;
        }

        using var transaction = connection.BeginTransaction(deferred: false);
        await using var countCommand = connection.CreateCommand();
        countCommand.Transaction = transaction;
        // The import runs at most once per database, so deleting every event does not bring legacy data back.
        countCommand.CommandText = """
            SELECT EXISTS (SELECT 1 FROM StorageMetadata WHERE Key = 'LegacyJsonLinesImported')
                OR EXISTS (SELECT 1 FROM CanonicalEvents);
            """;
        var alreadyHandled = (long)(await countCommand.ExecuteScalarAsync(cancellationToken) ?? 0L) != 0;

        await using var markerCommand = connection.CreateCommand();
        markerCommand.Transaction = transaction;
        markerCommand.CommandText = """
            INSERT OR IGNORE INTO StorageMetadata (Key, Value) VALUES ('LegacyJsonLinesImported', $importedAt);
            """;
        markerCommand.Parameters.AddWithValue("$importedAt", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await markerCommand.ExecuteNonQueryAsync(cancellationToken);

        if (alreadyHandled)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = """
            INSERT OR IGNORE INTO CanonicalEvents (
                Id, TimestampUtcTicks, OffsetTicks, SourceType, SourceName,
                Category, Action, Result, UserName, Device, SourceIp, RawContent, Attributes)
            VALUES (
                $id, $timestampUtcTicks, $offsetTicks, $sourceType, $sourceName,
                $category, $action, $result, $userName, $device, $sourceIp, $rawContent, $attributes);
            """;

        foreach (var path in _legacyJsonLinesPaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var lineNumber = 0;
            await foreach (var line in File.ReadLinesAsync(path, cancellationToken))
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                CanonicalEvent? item;
                try
                {
                    item = JsonSerializer.Deserialize<CanonicalEvent>(line, JsonOptions);
                }
                catch (JsonException exception)
                {
                    throw new InvalidDataException(
                        $"Could not migrate legacy event data from '{path}' at line {lineNumber}.", exception);
                }

                if (item is null)
                {
                    continue;
                }

                insertCommand.Parameters.Clear();
                AddEventParameters(insertCommand, item);
                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static void AddEventParameters(SqliteCommand command, CanonicalEvent item)
    {
        command.Parameters.AddWithValue("$id", item.Id.ToString("D"));
        command.Parameters.AddWithValue("$timestampUtcTicks", item.Timestamp.UtcTicks);
        command.Parameters.AddWithValue("$offsetTicks", item.Timestamp.Offset.Ticks);
        command.Parameters.AddWithValue("$sourceType", item.SourceType);
        command.Parameters.AddWithValue("$sourceName", item.SourceName);
        command.Parameters.AddWithValue("$category", item.Category);
        command.Parameters.AddWithValue("$action", item.Action);
        command.Parameters.AddWithValue("$result", (object?)item.Result ?? DBNull.Value);
        command.Parameters.AddWithValue("$userName", (object?)item.User ?? DBNull.Value);
        command.Parameters.AddWithValue("$device", (object?)item.Device ?? DBNull.Value);
        command.Parameters.AddWithValue("$sourceIp", (object?)item.SourceIp ?? DBNull.Value);
        command.Parameters.AddWithValue("$rawContent", item.RawContent);
        command.Parameters.AddWithValue("$attributes", item.Attributes is { Count: > 0 }
            ? JsonSerializer.Serialize(item.Attributes, JsonOptions)
            : DBNull.Value);
    }
}
