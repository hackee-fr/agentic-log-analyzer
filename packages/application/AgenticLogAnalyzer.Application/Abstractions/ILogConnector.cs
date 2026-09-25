using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Application.Abstractions;

public interface ILogConnector
{
    string Name { get; }

    IAsyncEnumerable<RawLog> ReadAsync(
        CancellationToken cancellationToken);
}
