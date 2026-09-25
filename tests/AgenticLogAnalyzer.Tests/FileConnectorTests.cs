using AgenticLogAnalyzer.Connectors;
using Xunit;

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
            var logs = new List<string>();

            await foreach (var rawLog in connector.ReadAsync(CancellationToken.None))
            {
                logs.Add(rawLog.Content);
            }

            Assert.Equal(["first log", "second log"], logs);
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

            await Assert.ThrowsAsync<OperationCanceledException>(
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
