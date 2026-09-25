using System.Runtime.CompilerServices;
using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Connectors;

public sealed class SampleLogConnector : ILogConnector
{
    public string Name => "sample";

    public async IAsyncEnumerable<RawLog> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var receivedAt = DateTimeOffset.UtcNow;

        cancellationToken.ThrowIfCancellationRequested();

        yield return new RawLog(
            Name,
            Name,
            "2026-09-25T08:42:01Z|authentication|login|failure|john.doe|reader-01|192.168.1.50",
            receivedAt);

        cancellationToken.ThrowIfCancellationRequested();

        yield return new RawLog(
            Name,
            Name,
            "2026-09-25T08:43:12Z|authentication|login|success|john.doe|reader-01|192.168.1.50",
            receivedAt);
    }
}
