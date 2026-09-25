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
                Category, Action, Result, UserName, Device, SourceIp, RawContent)
            VALUES (
                $id, $timestampUtcTicks, $offsetTicks, $sourceType, $sourceName,
                $category, $action, $result, $userName, $device, $sourceIp, $rawContent)
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
                RawContent = excluded.RawContent;
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
                   Category, Action, Result, UserName, Device, SourceIp, RawContent
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
                reader.GetString(11)));
        }

        return events;
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
                    RawContent TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_CanonicalEvents_TimestampUtcTicks
                    ON CanonicalEvents (TimestampUtcTicks DESC);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            await ImportLegacyJsonLinesIfEmptyAsync(connection, cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

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
        countCommand.CommandText = "SELECT COUNT(*) FROM CanonicalEvents;";
        if ((long)(await countCommand.ExecuteScalarAsync(cancellationToken) ?? 0L) != 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = """
            INSERT OR IGNORE INTO CanonicalEvents (
                Id, TimestampUtcTicks, OffsetTicks, SourceType, SourceName,
                Category, Action, Result, UserName, Device, SourceIp, RawContent)
            VALUES (
                $id, $timestampUtcTicks, $offsetTicks, $sourceType, $sourceName,
                $category, $action, $result, $userName, $device, $sourceIp, $rawContent);
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
    }
}
