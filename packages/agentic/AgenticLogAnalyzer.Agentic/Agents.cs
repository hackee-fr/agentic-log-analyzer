using AgenticLogAnalyzer.Correlation;
using AgenticLogAnalyzer.Detection;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Agentic;

public sealed class InvestigationAgent(SearchEventsTool searchEventsTool)
{
    public async Task<IReadOnlyCollection<CanonicalEvent>> ExecuteAsync(
        string query,
        int maxEvents,
        CancellationToken cancellationToken) =>
        await searchEventsTool.ExecuteAsync(query, maxEvents, cancellationToken);
}

public sealed class DetectionAgent(RunDetectionsTool runDetectionsTool)
{
    public IReadOnlyCollection<DetectionFinding> Execute(IEnumerable<CanonicalEvent> events) =>
        runDetectionsTool.Execute(events);
}

public sealed class CorrelationAgent(CorrelateEventsTool correlateEventsTool)
{
    public IReadOnlyCollection<EventCorrelation> Execute(IEnumerable<CanonicalEvent> events) =>
        correlateEventsTool.Execute(events);
}

public sealed class ReportingAgent
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public InvestigationReport Execute(
        string query,
        IReadOnlyCollection<CanonicalEvent> events,
        IReadOnlyCollection<DetectionFinding> detections,
        IReadOnlyCollection<EventCorrelation> correlations,
        IReadOnlyCollection<AgentStep> agentTrace)
    {
        var facts = detections.Select(item => new InvestigationFact(
            $"{item.Title}: {item.Description}",
            item.EvidenceEventIds)).ToArray();
        var hypotheses = detections
            .Where(item => item.RuleId == "AUTH-001")
            .Select(item => new InvestigationHypothesis(
                $"The repeated authentication failures for {item.User ?? "the observed identity"} may indicate an unauthorized access attempt.",
                item.EvidenceEventIds))
            .ToArray();
        var recommendations = detections.Count > 0
            ? new[]
            {
                "Verify with the account owner whether the failed attempts were expected.",
                "Review authentication controls and related events for the identities and source IPs listed in the evidence."
            }
            : Array.Empty<string>();
        var summary = events.Count == 0
            ? "No events matched the investigation query."
            : $"Reviewed {events.Count} event(s), found {detections.Count} deterministic detection(s), and built {correlations.Count} correlation(s).";

        return new InvestigationReport(
            Guid.NewGuid(),
            _timeProvider.GetUtcNow(),
            query,
            summary,
            events,
            detections,
            correlations,
            facts,
            hypotheses,
            recommendations,
            agentTrace,
            LlmUsed: false);
    }
}
