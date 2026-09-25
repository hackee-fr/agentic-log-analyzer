using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Correlation;
using AgenticLogAnalyzer.Detection;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Agentic;

/// <summary>Deterministic, bounded access to canonical events.</summary>
public sealed class SearchEventsTool(IEventRepository repository)
{
    public async Task<IReadOnlyCollection<CanonicalEvent>> ExecuteAsync(
        string query,
        int maxEvents,
        CancellationToken cancellationToken)
    {
        var resultLimit = Math.Clamp(maxEvents, 1, 1000);
        var events = await repository.SearchAsync(query, cancellationToken);
        return events.Take(resultLimit).ToArray();
    }
}

/// <summary>Runs registered deterministic detection rules over supplied events.</summary>
public sealed class RunDetectionsTool(IEnumerable<IDetectionRule> rules)
{
    public IReadOnlyCollection<DetectionFinding> Execute(IEnumerable<CanonicalEvent> events) =>
        rules.SelectMany(rule => rule.Evaluate(events)).ToArray();
}

/// <summary>Creates evidence links between events sharing canonical entities.</summary>
public sealed class CorrelateEventsTool(EventCorrelationService correlationService)
{
    public IReadOnlyCollection<EventCorrelation> Execute(IEnumerable<CanonicalEvent> events) =>
        correlationService.Correlate(events);
}
