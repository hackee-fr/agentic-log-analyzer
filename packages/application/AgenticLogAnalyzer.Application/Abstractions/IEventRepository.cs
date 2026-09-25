using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Application.Abstractions;

public interface IEventRepository
{
    Task SaveAsync(
        CanonicalEvent canonicalEvent,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CanonicalEvent>> SearchAsync(
        string query,
        CancellationToken cancellationToken);
}
