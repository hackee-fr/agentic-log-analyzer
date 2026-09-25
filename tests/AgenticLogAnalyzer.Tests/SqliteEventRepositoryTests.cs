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
}
