using AgenticLogAnalyzer.Application.Parsing;
using AgenticLogAnalyzer.Connectors;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Tests;

public sealed class FileConnectorTests
{
    [Fact]
    public async Task ReadAsync_IgnoresBlankLines_AndReturnsLogs()
    {
        var path = CreateTemporaryFile(
            "first log",
            "",
            "   ",
            "second log");

        try
        {
            var connector = new FileConnector(path);
            var logs = new List<RawLog>();

            await foreach (var rawLog in connector.ReadAsync(CancellationToken.None))
            {
                logs.Add(rawLog);
            }

            Assert.Equal(["first log", "second log"], logs.Select(l => l.Content));
            Assert.All(logs, l => Assert.Equal("file", l.Source));
            Assert.All(logs, l => Assert.Equal(path, l.SourceName));
        }
        finally
        {
            DeleteTemporaryFile(path);
        }
    }

    [Fact]
    public async Task ReadAsync_EmptyFile_ReturnsNoLogs()
    {
        var path = CreateTemporaryFile();

        try
        {
            var connector = new FileConnector(path);
            var logs = new List<string>();

            await foreach (var rawLog in connector.ReadAsync(CancellationToken.None))
            {
                logs.Add(rawLog.Content);
            }

            Assert.Empty(logs);
        }
        finally
        {
            DeleteTemporaryFile(path);
        }
    }

    [Fact]
    public async Task ReadAsync_MissingFile_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.log");

        var connector = new FileConnector(path);

        await Assert.ThrowsAsync<FileNotFoundException>(
            async () =>
            {
                await foreach (var _ in connector.ReadAsync(CancellationToken.None))
                {
                }
            });
    }

    [Fact]
    public async Task ReadAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var path = CreateTemporaryFile(
            "first log",
            "second log");

        try
        {
            using var cancellationTokenSource = new CancellationTokenSource();

            cancellationTokenSource.Cancel();

            var connector = new FileConnector(path);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () =>
                {
                    await foreach (var _ in connector.ReadAsync(
                        cancellationTokenSource.Token))
                    {
                    }
                });
        }
        finally
        {
            DeleteTemporaryFile(path);
        }
    }

    [Fact]
    public async Task ReadAsync_SampleFixture_ParsesAllLines()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "sample-pipe-log.txt");
        var connector = new FileConnector(path);
        var parser = new CanonicalEventParser();
        var events = new List<CanonicalEvent>();

        await foreach (var rawLog in connector.ReadAsync(CancellationToken.None))
        {
            events.Add(parser.Parse(rawLog));
        }

        Assert.Equal(2, events.Count);
        Assert.Equal(["failure", "success"], events.Select(e => e.Result));
    }

    private static string CreateTemporaryFile(params string[] lines)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.log");

        File.WriteAllLines(path, lines);

        return path;
    }

    private static void DeleteTemporaryFile(string path)
    {
        File.Delete(path);
    }
}
