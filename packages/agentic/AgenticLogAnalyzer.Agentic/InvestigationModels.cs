using AgenticLogAnalyzer.Correlation;
using AgenticLogAnalyzer.Detection;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Agentic;

public sealed record InvestigationRequest(string? Query = null, int? MaxEvents = null);

public sealed record InvestigationFact(string Statement, IReadOnlyCollection<Guid> EvidenceEventIds);

public sealed record InvestigationHypothesis(string Statement, IReadOnlyCollection<Guid> EvidenceEventIds);

public sealed record AgentStep(string Agent, string Outcome, int EvidenceCount);

public sealed record InvestigationReport(
    Guid Id,
    DateTimeOffset CreatedAt,
    string Query,
    string Summary,
    IReadOnlyCollection<CanonicalEvent> Events,
    IReadOnlyCollection<DetectionFinding> Detections,
    IReadOnlyCollection<EventCorrelation> Correlations,
    IReadOnlyCollection<InvestigationFact> Facts,
    IReadOnlyCollection<InvestigationHypothesis> Hypotheses,
    IReadOnlyCollection<string> Recommendations,
    IReadOnlyCollection<AgentStep> AgentTrace,
    bool LlmUsed);

internal sealed record InvestigationContext(
    string Query,
    IReadOnlyCollection<CanonicalEvent> Events,
    IReadOnlyCollection<DetectionFinding> Detections,
    IReadOnlyCollection<EventCorrelation> Correlations);
