using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Application.Parsing;
using AgenticLogAnalyzer.Agentic;
using AgenticLogAnalyzer.Connectors;
using AgenticLogAnalyzer.Correlation;
using AgenticLogAnalyzer.Detection;
using AgenticLogAnalyzer.Domain.Logs;
using AgenticLogAnalyzer.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var dataFile = builder.Configuration["Storage:FilePath"]
    ?? Path.Combine(Directory.GetCurrentDirectory(), "data", "events.jsonl");
var dashboardOrigins = new[]
{
    builder.Configuration["Dashboard:Origin"] ?? "http://localhost:5081",
    builder.Configuration["Dashboard:DevelopmentOrigin"] ?? "http://localhost:5173"
};

builder.Services.AddSingleton<ILogParser, CanonicalEventParser>();
builder.Services.AddSingleton<IEventRepository>(_ => new JsonLinesEventRepository(dataFile));
builder.Services.AddSingleton<IDetectionRule, RepeatedAuthenticationFailureRule>();
builder.Services.AddSingleton<EventCorrelationService>();
builder.Services.AddSingleton<SearchEventsTool>();
builder.Services.AddSingleton<RunDetectionsTool>();
builder.Services.AddSingleton<CorrelateEventsTool>();
builder.Services.AddSingleton<InvestigationAgent>();
builder.Services.AddSingleton<DetectionAgent>();
builder.Services.AddSingleton<CorrelationAgent>();
builder.Services.AddSingleton<ReportingAgent>();
builder.Services.AddSingleton<InvestigationOrchestrator>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(dashboardOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "agentic-log-analyzer-api" }));
app.MapGet("/api/info", () => Results.Ok(new
{
    name = "Agentic Log Analyzer",
    version = "0.2.0",
    deterministicEngineReady = true,
    llmEnabled = false
}));

app.MapGet("/api/events", async (string? q, IEventRepository repository, CancellationToken token) =>
    Results.Ok(await repository.SearchAsync(q ?? string.Empty, token)));

app.MapPost("/api/investigations", async (
    InvestigationRequest request,
    InvestigationOrchestrator orchestrator,
    CancellationToken token) =>
    Results.Ok(await orchestrator.RunAsync(request, token)));

app.MapPost("/api/ingest", async Task<IResult> (
    IngestRequest request,
    ILogParser parser,
    IEventRepository repository,
    CancellationToken token) =>
{
    if (string.IsNullOrWhiteSpace(request.Content))
    {
        return Results.BadRequest(new { error = "Provide at least one pipe-delimited log line." });
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

app.Run();

internal sealed record IngestRequest(string Content, string? Source, string? SourceName);
internal sealed record RejectedLine(int Line, string Reason);
