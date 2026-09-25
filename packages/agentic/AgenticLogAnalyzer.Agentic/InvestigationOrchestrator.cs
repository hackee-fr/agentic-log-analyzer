namespace AgenticLogAnalyzer.Agentic;

/// <summary>Coordinates deterministic investigation agents; no LLM output is used as evidence.</summary>
public sealed class InvestigationOrchestrator(
    InvestigationAgent investigationAgent,
    DetectionAgent detectionAgent,
    CorrelationAgent correlationAgent,
    ReportingAgent reportingAgent)
{
    public async Task<InvestigationReport> RunAsync(
        InvestigationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var query = request.Query?.Trim() ?? string.Empty;
        var events = await investigationAgent.ExecuteAsync(query, request.MaxEvents ?? 500, cancellationToken);
        var detections = detectionAgent.Execute(events);
        var correlations = correlationAgent.Execute(events);
        var trace = new[]
        {
            new AgentStep("InvestigationAgent", "completed", events.Count),
            new AgentStep("DetectionAgent", "completed", detections.Count),
            new AgentStep("CorrelationAgent", "completed", correlations.Count),
            new AgentStep("ReportingAgent", "completed", detections.Count)
        };

        return reportingAgent.Execute(query, events, detections, correlations, trace);
    }
}
