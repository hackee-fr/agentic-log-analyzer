using AgenticLogAnalyzer.Agentic;
using AgenticLogAnalyzer.Agentic.Chat;
using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Application.Parsing;
using AgenticLogAnalyzer.Detection;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Tests;

public sealed class LogChatServiceTests
{
    private static readonly string[] Lines =
    [
        "2026-09-25T17:00:03.017Z INFO  [auth] User authentication successful user_id=1042 source=192.168.1.42",
        "2026-09-25T17:00:23.118Z ERROR [database] Query execution failed error=\"timeout expired\" query_id=q_71291",
        "2026-09-25T17:00:30.118Z WARN  [security] Failed authentication attempt username=admin source=10.10.20.15",
        "2026-09-25T17:00:31.447Z WARN  [security] Failed authentication attempt username=admin source=10.10.20.15",
        "2026-09-25T17:00:32.771Z WARN  [security] Failed authentication attempt username=admin source=10.10.20.15",
        "2026-09-25T17:00:33.092Z ERROR [security] Account temporarily locked username=admin reason=too_many_failed_attempts",
        "2026-09-25T17:00:46.517Z ERROR [docker] Container restart detected container=worker-02 exit_code=137",
        "2026-09-25T17:00:52.116Z WARN  [monitoring] CPU usage host=server-01 value=91%"
    ];

    [Theory]
    [InlineData("Y a-t-il des attaques ?", ChatIntent.Security, null)]
    [InlineData("Quelles IP ont échoué ?", ChatIntent.TopSourceIps, "failure")]
    [InlineData("Quels utilisateurs apparaissent ?", ChatIntent.TopUsers, null)]
    [InlineData("Chronologie des avertissements", ChatIntent.Timeline, "warning")]
    [InlineData("Montre les erreurs docker", ChatIntent.List, "failure")]
    [InlineData("Fais-moi un résumé", ChatIntent.Summary, null)]
    public void Analyze_DetectsIntentAndResultFilter(string question, ChatIntent intent, string? resultFilter)
    {
        var query = ChatQuestionAnalyzer.Analyze(question);

        Assert.Equal(intent, query.Intent);
        Assert.Equal(resultFilter, query.ResultFilter);
    }

    [Fact]
    public void Analyze_KeepsEntityTermsAndDropsStopWords()
    {
        var query = ChatQuestionAnalyzer.Analyze("Que s'est-il passé avec l'IP 10.10.20.15 ?");

        Assert.Equal(ChatIntent.TopSourceIps, query.Intent);
        Assert.Equal(["10.10.20.15"], query.Terms);
    }

    [Fact]
    public async Task AskAsync_Security_ReturnsBruteForceDetectionWithEvidence()
    {
        var answer = await CreateService().AskAsync(new ChatRequest("Y a-t-il une attaque ?"), CancellationToken.None);

        var detection = Assert.Single(answer.Detections);
        Assert.Equal("AUTH-001", detection.RuleId);
        Assert.Equal("admin", detection.User);
        Assert.All(detection.EvidenceEventIds, id => Assert.Contains(answer.Evidence, item => item.Id == id));
        Assert.Contains("10.10.20.15 (3)", answer.Answer, StringComparison.Ordinal);
        Assert.False(answer.LlmUsed);
    }

    [Fact]
    public async Task AskAsync_ErrorsForComponent_FiltersByTermAndResult()
    {
        var answer = await CreateService().AskAsync(new ChatRequest("Montre les erreurs docker"), CancellationToken.None);

        Assert.Equal(1, answer.MatchedEventCount);
        var item = Assert.Single(answer.Evidence);
        Assert.Equal("docker", item.Category);
        Assert.Contains("terme: docker", answer.AppliedFilters);
        Assert.Contains("résultat: failure", answer.AppliedFilters);
    }

    [Fact]
    public async Task AskAsync_UnknownTerm_IsIgnoredInsteadOfEmptyingTheScope()
    {
        var answer = await CreateService().AskAsync(new ChatRequest("Quelles erreurs sur le serveur ?"), CancellationToken.None);

        // WARN "Failed authentication attempt" lines are normalized as failures by the parser.
        Assert.Equal(6, answer.MatchedEventCount);
        Assert.DoesNotContain(answer.AppliedFilters, filter => filter.Contains("serveur", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AskAsync_EmptyRepository_ExplainsThatLogsMustBeImported()
    {
        var service = new LogChatService(new InMemoryEventRepository([]), CreateDetections(), new FakeLlm("unused"));

        var answer = await service.AskAsync(new ChatRequest("résumé"), CancellationToken.None);

        Assert.Contains("Importez des logs", answer.Answer, StringComparison.Ordinal);
        Assert.False(answer.LlmUsed);
    }

    [Fact]
    public async Task AskAsync_WithLlm_KeepsDeterministicAnswerAndEvidence()
    {
        var llm = new FakeLlm("Réponse reformulée");
        var answer = await CreateService(llm).AskAsync(new ChatRequest("Y a-t-il une attaque ?"), CancellationToken.None);

        Assert.True(answer.LlmUsed);
        Assert.Equal("Réponse reformulée", answer.Answer);
        Assert.Contains("AUTH-001", answer.DeterministicAnswer, StringComparison.Ordinal);
        Assert.Contains("10.10.20.15", llm.LastPrompt, StringComparison.Ordinal);
        Assert.NotEmpty(answer.Evidence);
    }

    [Fact]
    public async Task AskAsync_LlmFailure_FallsBackToDeterministicAnswer()
    {
        var answer = await CreateService(new FailingLlm()).AskAsync(new ChatRequest("résumé"), CancellationToken.None);

        Assert.False(answer.LlmUsed);
        Assert.Equal(answer.DeterministicAnswer, answer.Answer);
        Assert.Equal("connection refused", answer.LlmError);
    }

    private static LogChatService CreateService(ILlmProvider? llm = null)
    {
        var parser = new CanonicalEventParser();
        var events = Lines
            .Select(line => parser.Parse(new RawLog("test", "sample", line, DateTimeOffset.UtcNow)))
            .ToArray();
        return new LogChatService(new InMemoryEventRepository(events), CreateDetections(), llm);
    }

    private static RunDetectionsTool CreateDetections() => new([new RepeatedAuthenticationFailureRule()]);

    private sealed class FakeLlm(string response) : ILlmProvider
    {
        public string LastPrompt { get; private set; } = string.Empty;

        public Task<string> GenerateAsync(string systemPrompt, string prompt, CancellationToken cancellationToken)
        {
            LastPrompt = prompt;
            return Task.FromResult(response);
        }
    }

    private sealed class FailingLlm : ILlmProvider
    {
        public Task<string> GenerateAsync(string systemPrompt, string prompt, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused");
    }

    private sealed class InMemoryEventRepository(IReadOnlyCollection<CanonicalEvent> events) : IEventRepository
    {
        public Task SaveAsync(CanonicalEvent canonicalEvent, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyCollection<CanonicalEvent>> SearchAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult(events);
    }
}
