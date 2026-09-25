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

    [Fact]
    public async Task DeleteAsync_BySourceThenAll_RemovesMatchingEvents()
    {
        var path = Path.Combine(Path.GetTempPath(), $"events-{Guid.NewGuid():N}.jsonl");
        try
        {
            using var repository = new JsonLinesEventRepository(path);
            foreach (var sourceName in new[] { "first.log", "first.log", "second.log" })
            {
                await repository.SaveAsync(new CanonicalEvent(
                    Guid.NewGuid(), new DateTimeOffset(2026, 9, 25, 8, 42, 1, TimeSpan.Zero), "api", sourceName,
                    "system", "boot", null, null, null, null, "boot"), CancellationToken.None);
            }

            Assert.Equal(2, await repository.DeleteAsync("first.log", CancellationToken.None));
            Assert.Equal("second.log", Assert.Single(await repository.SearchAsync(string.Empty, CancellationToken.None)).SourceName);
            Assert.Equal(1, await repository.DeleteAsync(null, CancellationToken.None));
            Assert.Empty(await repository.SearchAsync(string.Empty, CancellationToken.None));
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
    public async Task SaveAndSearch_RoundTripsAttributes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"events-{Guid.NewGuid():N}.jsonl");
        try
        {
            using var repository = new JsonLinesEventRepository(path);
            await repository.SaveAsync(new CanonicalEvent(
                Guid.NewGuid(), new DateTimeOffset(2026, 9, 25, 8, 42, 1, TimeSpan.Zero), "api", "docker.log",
                "docker", "Container restart detected", "failure", null, "worker-02", null, "restart",
                new Dictionary<string, string> { ["exit_code"] = "137" }), CancellationToken.None);

            var stored = Assert.Single(await repository.SearchAsync(string.Empty, CancellationToken.None));

            Assert.Equal("137", stored.Attributes?["exit_code"]);
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

