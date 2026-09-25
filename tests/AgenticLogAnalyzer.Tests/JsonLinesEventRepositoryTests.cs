using AgenticLogAnalyzer.Domain.Logs;
using AgenticLogAnalyzer.Infrastructure;

namespace AgenticLogAnalyzer.Tests;

public sealed class JsonLinesEventRepositoryTests
{
    [Fact]
    public async Task SaveAndSearch_RoundTripsAndFiltersEvents()
    {
        var path = Path.Combine(Path.GetTempPath(), $"events-{Guid.NewGuid():N}.jsonl");
        try
        {
            using var repository = new JsonLinesEventRepository(path);
            var item = new CanonicalEvent(
                Guid.NewGuid(), new DateTimeOffset(2026, 9, 25, 8, 42, 1, TimeSpan.Zero), "file", "sample.log",
                "authentication", "login", "failure", "analyst", "workstation-1", "192.0.2.10",
                "authentication|login|failure|analyst");

            await repository.SaveAsync(item, CancellationToken.None);

            var matches = await repository.SearchAsync("analyst", CancellationToken.None);
            var noMatches = await repository.SearchAsync("unknown", CancellationToken.None);

            Assert.Equal(item, Assert.Single(matches));
            Assert.Empty(noMatches);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_SameEventTwice_IsReturnedOnce()
    {
        var path = Path.Combine(Path.GetTempPath(), $"events-{Guid.NewGuid():N}.jsonl");
        try
        {
            using var repository = new JsonLinesEventRepository(path);
            var item = new CanonicalEvent(
                Guid.NewGuid(), new DateTimeOffset(2026, 9, 25, 8, 42, 1, TimeSpan.Zero), "file", "sample.log",
                "authentication", "login", "failure", "analyst", null, null, "authentication|login|failure|analyst");

            await repository.SaveAsync(item, CancellationToken.None);
            await repository.SaveAsync(item, CancellationToken.None);

            Assert.Equal(item, Assert.Single(await repository.SearchAsync(string.Empty, CancellationToken.None)));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}

