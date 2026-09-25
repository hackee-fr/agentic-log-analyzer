using AgenticLogAnalyzer.Connectors;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Tests;

public sealed class TailingFileConnectorTests : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.log");

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    [Fact]
    public async Task ReadAsync_ReadsExistingThenAppendedLines()
    {
        await File.WriteAllTextAsync(_path, "first\n\n");
        using var cancellation = new CancellationTokenSource(Timeout);
        await using var logs = new TailingFileConnector(_path, PollInterval).ReadAsync(cancellation.Token).GetAsyncEnumerator();

        Assert.Equal("first", await NextAsync(logs));

        await File.AppendAllTextAsync(_path, "second\r\n", cancellation.Token);
        var second = await NextAsync(logs);

        Assert.Equal("second", second);
    }

    [Fact]
    public async Task ReadAsync_PartialLine_IsEmittedOnlyOnceCompleted()
    {
        await File.WriteAllTextAsync(_path, "par");
        using var cancellation = new CancellationTokenSource(Timeout);
        await using var logs = new TailingFileConnector(_path, PollInterval).ReadAsync(cancellation.Token).GetAsyncEnumerator();

        var next = NextAsync(logs);
        await Task.Delay(PollInterval * 5, cancellation.Token);
        Assert.False(next.IsCompleted);

        await File.AppendAllTextAsync(_path, "tial\n", cancellation.Token);

        Assert.Equal("partial", await next);
    }

    [Fact]
    public async Task ReadAsync_TruncatedFile_RestartsFromBeginning()
    {
        await File.WriteAllTextAsync(_path, "old line one\nold line two\n");
        using var cancellation = new CancellationTokenSource(Timeout);
        await using var logs = new TailingFileConnector(_path, PollInterval).ReadAsync(cancellation.Token).GetAsyncEnumerator();
        await NextAsync(logs);
        await NextAsync(logs);

        await File.WriteAllTextAsync(_path, "new\n", cancellation.Token);

        Assert.Equal("new", await NextAsync(logs));
    }

    [Fact]
    public async Task ReadAsync_MissingFile_WaitsUntilItExists()
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        await using var logs = new TailingFileConnector(_path, PollInterval).ReadAsync(cancellation.Token).GetAsyncEnumerator();

        var next = NextAsync(logs);
        await Task.Delay(PollInterval * 3, cancellation.Token);
        await File.WriteAllTextAsync(_path, "created later\n", cancellation.Token);

        Assert.Equal("created later", await next);
    }

    [Fact]
    public async Task ReadAsync_Cancelled_StopsWithOperationCanceledException()
    {
        await File.WriteAllTextAsync(_path, string.Empty);
        using var cancellation = new CancellationTokenSource();
        await using var logs = new TailingFileConnector(_path, PollInterval).ReadAsync(cancellation.Token).GetAsyncEnumerator();

        var next = logs.MoveNextAsync().AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => next);
    }

    private static async Task<string> NextAsync(IAsyncEnumerator<RawLog> logs)
    {
        Assert.True(await logs.MoveNextAsync());
        return logs.Current.Content;
    }
}
