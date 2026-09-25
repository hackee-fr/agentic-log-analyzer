using AgenticLogAnalyzer.Agentic;
using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Correlation;
using AgenticLogAnalyzer.Detection;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Tests;

public sealed class InvestigationOrchestratorTests
{
    [Fact]
    public async Task RunAsync_ProducesDetectionsCorrelationsAndTraceableEvidence()
    {
        var events = Enumerable.Range(0, 3)
            .Select(index => new CanonicalEvent(
                Guid.NewGuid(),
                new DateTimeOffset(2026, 9, 25, 17, 0, index, TimeSpan.Zero),
                "test",
                "sample",
                "authentication",
                "login",
                "failure",
                "admin",
                "workstation-1",
                "192.0.2.42",
                "failed login"))
            .ToArray();
        var orchestrator = new InvestigationOrchestrator(
            new InvestigationAgent(new SearchEventsTool(new InMemoryEventRepository(events))),
            new DetectionAgent(new RunDetectionsTool([new RepeatedAuthenticationFailureRule()])),
            new CorrelationAgent(new CorrelateEventsTool(new EventCorrelationService())),
            new ReportingAgent());

        var report = await orchestrator.RunAsync(new InvestigationRequest("admin"), CancellationToken.None);

        var detection = Assert.Single(report.Detections);
        Assert.Equal("AUTH-001", detection.RuleId);
        Assert.Equal(3, detection.EvidenceEventIds.Count);
        Assert.Contains(report.Correlations, item => item.EntityType == "user" && item.EntityValue == "admin");
        Assert.Equal(4, report.AgentTrace.Count);
        Assert.False(report.LlmUsed);
        Assert.Equal(detection.EvidenceEventIds.Order(), report.Facts.Single().EvidenceEventIds.Order());
    }

    private sealed class InMemoryEventRepository(IReadOnlyCollection<CanonicalEvent> events) : IEventRepository
    {
        public Task SaveAsync(CanonicalEvent canonicalEvent, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<int> DeleteAsync(string? sourceName, CancellationToken cancellationToken) => Task.FromResult(0);

        public Task<IReadOnlyCollection<CanonicalEvent>> SearchAsync(string query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyCollection<CanonicalEvent> matches = string.IsNullOrWhiteSpace(query)
                ? events
                : events.Where(item => item.RawContent.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || (item.User?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)).ToArray();
            return Task.FromResult(matches);
        }
    }
}
