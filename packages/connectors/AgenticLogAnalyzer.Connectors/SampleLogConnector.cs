using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Connectors;

public sealed class SampleLogConnector : ILogConnector
{
    public string Name => "sample";

    public Task<IReadOnlyCollection<RawLog>> ReadAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<RawLog> logs =
        [
            new RawLog(
                Name,
                "2026-09-25T08:42:01Z|authentication|login|failure|john.doe|reader-01|192.168.1.50",
                DateTimeOffset.UtcNow),
            new RawLog(
                Name,
                "2026-09-25T08:43:12Z|authentication|login|success|john.doe|reader-01|192.168.1.50",
                DateTimeOffset.UtcNow)
        ];

        return Task.FromResult(logs);
    }
}
