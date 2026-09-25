using AgenticLogAnalyzer.Agentic;
using AgenticLogAnalyzer.Agentic.Chat;
using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Application.Parsing;
using AgenticLogAnalyzer.Detection;
using AgenticLogAnalyzer.Domain.Logs;
using AgenticLogAnalyzer.Llm;
using Xunit.Abstractions;

namespace AgenticLogAnalyzer.Tests;

/// <summary>
/// Runs against a real Ollama server only when OLLAMA_INTEGRATION_URL is set, for example after
/// <c>docker compose -f infrastructure/docker/docker-compose.yml up -d ollama ollama-pull</c>:
/// <c>OLLAMA_INTEGRATION_URL=http://localhost:11434 dotnet test --filter OllamaIntegrationTests</c>.
/// Without the variable the tests return immediately.
/// </summary>
public sealed class OllamaIntegrationTests(ITestOutputHelper output)
{
    private static readonly string? BaseUrl = Environment.GetEnvironmentVariable("OLLAMA_INTEGRATION_URL");
    private static readonly string Model = Environment.GetEnvironmentVariable("OLLAMA_INTEGRATION_MODEL") ?? "llama3.2";

    [Fact]
    public async Task Status_ReportsInstalledModel()
    {
        if (BaseUrl is null)
        {
            return;
        }

        using var client = new HttpClient();
        var status = await new OllamaLlmProvider(client, new OllamaOptions(new Uri(BaseUrl), Model)).GetStatusAsync(CancellationToken.None);

        output.WriteLine($"reachable={status.Reachable} model={status.ModelAvailable} installed={string.Join(",", status.InstalledModels)}");
        Assert.True(status.Reachable, status.Error);
        Assert.True(status.ModelAvailable, status.Error);
    }

    [Fact]
    public async Task Chat_WithRealModel_ReturnsVerifiedRewordingOrDeterministicFallback()
    {
        if (BaseUrl is null)
        {
            return;
        }

        string[] lines =
        [
            "2026-09-25T17:00:30.118Z WARN  [security] Failed authentication attempt username=admin source=10.10.20.15",
            "2026-09-25T17:00:31.447Z WARN  [security] Failed authentication attempt username=admin source=10.10.20.15",
            "2026-09-25T17:00:32.771Z WARN  [security] Failed authentication attempt username=admin source=10.10.20.15",
            "2026-09-25T17:00:33.092Z ERROR [security] Account temporarily locked username=admin reason=too_many_failed_attempts"
        ];
        var parser = new CanonicalEventParser();
        var events = lines.Select(line => parser.Parse(new RawLog("test", "integration", line, DateTimeOffset.UtcNow))).ToArray();
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        var service = new LogChatService(
            new StaticRepository(events),
            new RunDetectionsTool([new RepeatedAuthenticationFailureRule()]),
            new OllamaLlmProvider(client, new OllamaOptions(new Uri(BaseUrl), Model)));

        var answer = await service.AskAsync(new ChatRequest("Y a-t-il une attaque ?"), CancellationToken.None);

        output.WriteLine($"llmUsed={answer.LlmUsed} llmError={answer.LlmError}");
        output.WriteLine(answer.Answer);
        Assert.False(string.IsNullOrWhiteSpace(answer.Answer));
        Assert.True(answer.LlmUsed || answer.LlmError is not null);
        Assert.Single(answer.Detections);
    }

    private sealed class StaticRepository(IReadOnlyCollection<CanonicalEvent> events) : IEventRepository
    {
        public Task SaveAsync(CanonicalEvent canonicalEvent, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyCollection<CanonicalEvent>> SearchAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult(events);

        public Task<int> DeleteAsync(string? sourceName, CancellationToken cancellationToken) => Task.FromResult(0);
    }
}
