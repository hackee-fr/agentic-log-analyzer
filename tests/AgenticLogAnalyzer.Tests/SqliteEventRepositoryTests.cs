using AgenticLogAnalyzer.Application.Parsing;
using AgenticLogAnalyzer.Domain.Logs;
using AgenticLogAnalyzer.Infrastructure;
using Microsoft.Data.Sqlite;

namespace AgenticLogAnalyzer.Tests;

public sealed class SqliteEventRepositoryTests
{
    [Fact]
    public async Task SaveAsync_ReingestedLine_IsStoredOnce()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"events-{Guid.NewGuid():N}");
        try
        {
            using var repository = new SqliteEventRepository(Path.Combine(directory, "events.sqlite3"));
            var parser = new CanonicalEventParser();
            const string line = "2026-09-25T17:00:30.118Z WARN  [security] Failed authentication attempt username=admin source=10.10.20.15";

            foreach (var receivedAt in new[] { DateTimeOffset.UnixEpoch, DateTimeOffset.UtcNow })
            {
                var item = parser.Parse(new RawLog("api", "log-test.txt", line, receivedAt));
                await repository.SaveAsync(item, CancellationToken.None);
            }

            var stored = Assert.Single(await repository.SearchAsync(string.Empty, CancellationToken.None));
            Assert.Equal(line, stored.RawContent);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DeleteAsync_BySource_RemovesOnlyThatSource()
    {
        var directory = CreateDirectory();
        try
        {
            using var repository = new SqliteEventRepository(Path.Combine(directory, "events.sqlite3"));
            await repository.SaveAsync(CreateEvent("first.log", "line one"), CancellationToken.None);
            await repository.SaveAsync(CreateEvent("first.log", "line two"), CancellationToken.None);
            await repository.SaveAsync(CreateEvent("second.log", "line three"), CancellationToken.None);

            var deleted = await repository.DeleteAsync("first.log", CancellationToken.None);

            Assert.Equal(2, deleted);
            var remaining = Assert.Single(await repository.SearchAsync(string.Empty, CancellationToken.None));
            Assert.Equal("second.log", remaining.SourceName);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task DeleteAsync_All_DoesNotReimportLegacyJsonLinesOnRestart()
    {
        var directory = CreateDirectory();
        try
        {
            var databasePath = Path.Combine(directory, "events.sqlite3");
            var legacyPath = Path.Combine(directory, "events.jsonl");
            using (var legacy = new JsonLinesEventRepository(legacyPath))
            {
                await legacy.SaveAsync(CreateEvent("legacy.log", "legacy line"), CancellationToken.None);
            }

            using (var repository = new SqliteEventRepository(databasePath, [legacyPath]))
            {
                Assert.Single(await repository.SearchAsync(string.Empty, CancellationToken.None));
                Assert.Equal(1, await repository.DeleteAsync(null, CancellationToken.None));
            }

            using var restarted = new SqliteEventRepository(databasePath, [legacyPath]);
            Assert.Empty(await restarted.SearchAsync(string.Empty, CancellationToken.None));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static CanonicalEvent CreateEvent(string sourceName, string content) =>
        new(Guid.NewGuid(), new DateTimeOffset(2026, 9, 25, 17, 0, 0, TimeSpan.Zero), "api", sourceName,
            "security", "login", "failure", null, null, null, content);

    private static string CreateDirectory() => Path.Combine(Path.GetTempPath(), $"events-{Guid.NewGuid():N}");

    private static void DeleteDirectory(string directory)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
