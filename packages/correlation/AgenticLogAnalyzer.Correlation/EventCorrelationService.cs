using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Correlation;

public sealed record EventCorrelation(
    string EntityType,
    string EntityValue,
    IReadOnlyCollection<Guid> EventIds,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen);

/// <summary>Groups events that share a user, source IP, or device.</summary>
public sealed class EventCorrelationService
{
    private readonly StringComparer _entityComparer = StringComparer.OrdinalIgnoreCase;

    public IReadOnlyCollection<EventCorrelation> Correlate(IEnumerable<CanonicalEvent> events)
    {
        var items = events.ToArray();
        var correlations = new List<EventCorrelation>();
        AddCorrelations(correlations, items, "user", item => item.User);
        AddCorrelations(correlations, items, "sourceIp", item => item.SourceIp);
        AddCorrelations(correlations, items, "device", item => item.Device);

        return correlations
            .OrderByDescending(item => item.EventIds.Count)
            .ThenBy(item => item.EntityType, StringComparer.Ordinal)
            .ThenBy(item => item.EntityValue, StringComparer.Ordinal)
            .ToArray();
    }

    private void AddCorrelations(
        List<EventCorrelation> target,
        IEnumerable<CanonicalEvent> events,
        string entityType,
        Func<CanonicalEvent, string?> selector)
    {
        foreach (var group in events
            .Select(item => (Event: item, Value: selector(item)))
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .GroupBy(item => item.Value!, _entityComparer)
            .Where(group => group.Count() > 1))
        {
            var matches = group.Select(item => item.Event).ToArray();
            target.Add(new EventCorrelation(
                entityType,
                group.Key,
                matches.Select(item => item.Id).Distinct().ToArray(),
                matches.Min(item => item.Timestamp),
                matches.Max(item => item.Timestamp)));
        }
    }
}
