using System.Runtime.CompilerServices;
using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Connectors;

public sealed class FileConnector(string path) : ILogConnector
{
    private readonly string _path = path;

    public string Name => "file";

    /// <remarks>
    /// All lines of one read share the same <see cref="RawLog.ReceivedAt"/>:
    /// the time the file was ingested, not the time each line was written.
    /// </remarks>
    public async IAsyncEnumerable<RawLog> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var receivedAt = DateTimeOffset.UtcNow;

        await foreach (var line in File.ReadLinesAsync(
            _path,
            cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            yield return new RawLog(
                Name,
                _path,
                line,
                receivedAt);
        }
    }
}
