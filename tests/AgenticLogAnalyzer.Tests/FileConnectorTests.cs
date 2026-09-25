using AgenticLogAnalyzer.Connectors;

namespace AgenticLogAnalyzer.Tests;

public sealed class FileConnectorTests
{
    [Fact]
    public async Task ReadAsync_IgnoresBlankLines_AndReturnsLogs()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.log");

        try
        {
            await File.WriteAllLinesAsync(path, [
                "first log",
                "",
                "   ",
                "second log"
            ]);

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
            File.Delete(path);
        }
    }
}
