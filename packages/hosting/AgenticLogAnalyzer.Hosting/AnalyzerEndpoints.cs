using AgenticLogAnalyzer.Agentic;
using AgenticLogAnalyzer.Agentic.Chat;
using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Domain.Logs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AgenticLogAnalyzer.Hosting;

public static class AnalyzerEndpoints
{
    /// <summary>Maps the HTTP API used by the dashboard (health, info, settings, events, chat, investigations, ingest).</summary>
    public static IEndpointRouteBuilder MapLogAnalyzerApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "agentic-log-analyzer-api" }));

        app.MapGet("/api/info", (AnalyzerHostOptions options) => Results.Ok(new
        {
            name = "Agentic Log Analyzer",
            version = "0.2.0",
            deterministicEngineReady = true,
            llmEnabled = options.LlmEnabled
        }));

        app.MapGet("/api/settings", (AnalyzerHostOptions options, IHostEnvironment environment) => Results.Ok(new
        {
            environment = environment.EnvironmentName,
            storage = new
            {
                provider = options.StorageProvider,
                fileName = Path.GetFileName(options.StoragePath),
                initialized = File.Exists(options.StoragePath)
            },
            analysis = new
            {
                deterministicEngineEnabled = true,
                llmEnabled = options.LlmEnabled,
                llmProvider = options.LlmEnabled ? "Ollama" : null,
                llmModel = options.LlmEnabled ? options.OllamaModel : null
            }
        }));

        app.MapGet("/api/llm/status", async Task<IResult> (
            IServiceProvider services,
            AnalyzerHostOptions options,
            CancellationToken token) =>
        {
            var statusProvider = services.GetService<ILlmStatusProvider>();
            if (statusProvider is null)
            {
                return Results.Ok(new { enabled = false, status = (LlmStatus?)null });
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                return Results.Ok(new { enabled = true, status = await statusProvider.GetStatusAsync(timeout.Token) });
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                return Results.Ok(new
                {
                    enabled = true,
                    status = new LlmStatus("Ollama", options.OllamaModel, false, false, [], "Ollama did not answer within 5 seconds.")
                });
            }
        });

        app.MapGet("/api/events", async (string? q, IEventRepository repository, CancellationToken token) =>
            Results.Ok(await repository.SearchAsync(q ?? string.Empty, token)));

        app.MapDelete("/api/events", async Task<IResult> (
            string? source,
            bool? all,
            IEventRepository repository,
            CancellationToken token) =>
        {
            if (!string.IsNullOrWhiteSpace(source))
            {
                return Results.Ok(new { deleted = await repository.DeleteAsync(source, token), source });
            }

            return all == true
                ? Results.Ok(new { deleted = await repository.DeleteAsync(null, token), source = (string?)null })
                : Results.BadRequest(new { error = "Specify ?source=<source name> or ?all=true." });
        });

        app.MapPost("/api/investigations", async (
            InvestigationRequest request,
            InvestigationOrchestrator orchestrator,
            CancellationToken token) =>
            Results.Ok(await orchestrator.RunAsync(request, token)));

        app.MapPost("/api/chat", async Task<IResult> (
            ChatRequest request,
            LogChatService chat,
            CancellationToken token) =>
            string.IsNullOrWhiteSpace(request.Question)
                ? Results.BadRequest(new { error = "Ask a question about your logs." })
                : Results.Ok(await chat.AskAsync(request, token)));

        app.MapPost("/api/ingest", async Task<IResult> (
            IngestRequest request,
            ILogParser parser,
            IEventRepository repository,
            CancellationToken token) =>
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return Results.BadRequest(new { error = "Provide at least one log line." });
            }

            var accepted = 0;
            var rejected = new List<RejectedLine>();
            var source = string.IsNullOrWhiteSpace(request.Source) ? "api" : request.Source.Trim();
            var sourceName = string.IsNullOrWhiteSpace(request.SourceName) ? source : request.SourceName.Trim();

            foreach (var (line, index) in request.Content.Split('\n').Select((value, index) => (value.TrimEnd('\r'), index + 1)))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var raw = new RawLog(source, sourceName, line, DateTimeOffset.UtcNow);
                try
                {
                    var item = parser.Parse(raw);
                    await repository.SaveAsync(item, token);
                    accepted++;
                }
                catch (FormatException exception)
                {
                    rejected.Add(new RejectedLine(index, exception.Message));
                }
            }

            return Results.Ok(new { accepted, rejected });
        });

        return app;
    }

    internal sealed record IngestRequest(string Content, string? Source, string? SourceName);

    internal sealed record RejectedLine(int Line, string Reason);
}
