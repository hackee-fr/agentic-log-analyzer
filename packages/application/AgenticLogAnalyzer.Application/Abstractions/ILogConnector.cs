using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Application.Abstractions;

public interface ILogConnector
{
    string Name { get; }

    Task<IReadOnlyCollection<RawLog>> ReadAsync(
        CancellationToken cancellationToken);
}
