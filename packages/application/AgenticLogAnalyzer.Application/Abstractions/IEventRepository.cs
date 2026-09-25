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

    /// <summary>
    /// Deletes the events imported from <paramref name="sourceName"/> (exact, case-sensitive match),
    /// or every stored event when it is null. Returns the number of deleted events.
    /// </summary>
    Task<int> DeleteAsync(
        string? sourceName,
        CancellationToken cancellationToken);
}
