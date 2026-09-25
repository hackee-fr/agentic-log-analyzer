using AgenticLogAnalyzer.Agentic;
using AgenticLogAnalyzer.Agentic.Chat;
using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Application.Parsing;
using AgenticLogAnalyzer.Connectors;
using AgenticLogAnalyzer.Correlation;
using AgenticLogAnalyzer.Detection;
using AgenticLogAnalyzer.Domain.Logs;
using AgenticLogAnalyzer.Infrastructure;
using AgenticLogAnalyzer.Llm;

var builder = WebApplication.CreateBuilder(args);
var storageProvider = builder.Configuration["Storage:Provider"] ?? "sqlite";
var sqlitePath = builder.Configuration["Storage:SqlitePath"]
    ?? DevelopmentStoragePaths.GetDatabasePath();
var legacyJsonLinesPaths = DevelopmentStoragePaths.GetLegacyJsonLinesPaths();
var jsonLinesPath = builder.Configuration["Storage:FilePath"]
    ?? Path.Combine(Directory.GetCurrentDirectory(), "data", "events.jsonl");
var dashboardOrigins = new[]
{
    builder.Configuration["Dashboard:Origin"] ?? "http://localhost:5081",
    builder.Configuration["Dashboard:DevelopmentOrigin"] ?? "http://localhost:5173"
};
var llmEnabled = string.Equals(builder.Configuration["Llm:Provider"], "ollama", StringComparison.OrdinalIgnoreCase);

builder.Services.AddSingleton<ILogParser, CanonicalEventParser>();
builder.Services.AddSingleton<IEventRepository>(_ => storageProvider.ToLowerInvariant() switch
{
    "sqlite" => new SqliteEventRepository(sqlitePath, legacyJsonLinesPaths),
    "jsonl" => new JsonLinesEventRepository(jsonLinesPath),
    _ => throw new InvalidOperationException($"Unsupported storage provider '{storageProvider}'. Use 'sqlite' or 'jsonl'.")
});
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
builder.Services.AddScoped<LogChatService>();
if (llmEnabled)
{
    builder.Services.AddSingleton(new OllamaOptions(
        new Uri(builder.Configuration["Llm:Ollama:BaseUrl"] ?? "http://localhost:11434"),
        builder.Configuration["Llm:Ollama:Model"] ?? "llama3.2",
        builder.Configuration["Llm:Ollama:KeepAlive"] ?? "15m",
        builder.Configuration.GetValue("Llm:Ollama:MaxOutputTokens", 350)));
    // CPU inference in Docker can be slow, especially while the model loads on the first request.
    var llmTimeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Llm:Ollama:TimeoutSeconds", 180));
    builder.Services.AddHttpClient<OllamaLlmProvider>(client => client.Timeout = llmTimeout);
    builder.Services.AddTransient<ILlmProvider>(services => services.GetRequiredService<OllamaLlmProvider>());
    builder.Services.AddTransient<ILlmStatusProvider>(services => services.GetRequiredService<OllamaLlmProvider>());
}

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
    llmEnabled
}));

app.MapGet("/api/settings", () => Results.Ok(new
{
    environment = app.Environment.EnvironmentName,
    storage = new
    {
        provider = storageProvider,
        fileName = Path.GetFileName(storageProvider.Equals("sqlite", StringComparison.OrdinalIgnoreCase)
            ? sqlitePath
            : jsonLinesPath),
        initialized = File.Exists(storageProvider.Equals("sqlite", StringComparison.OrdinalIgnoreCase)
            ? sqlitePath
            : jsonLinesPath)
    },
    analysis = new
    {
        deterministicEngineEnabled = true,
        llmEnabled,
        llmProvider = llmEnabled ? "Ollama" : null,
        llmModel = llmEnabled ? builder.Configuration["Llm:Ollama:Model"] ?? "llama3.2" : null
    }
}));

app.MapGet("/api/llm/status", async Task<IResult> (IServiceProvider services, CancellationToken token) =>
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
            status = new LlmStatus("Ollama", builder.Configuration["Llm:Ollama:Model"] ?? "llama3.2", false, false, [], "Ollama did not answer within 5 seconds.")
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
