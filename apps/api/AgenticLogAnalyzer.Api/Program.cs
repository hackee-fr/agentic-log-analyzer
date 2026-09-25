var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "agentic-log-analyzer-api"
}));

app.MapGet("/api/info", () => Results.Ok(new
{
    name = "Agentic Log Analyzer",
    version = "0.1.0",
    deterministicEngineReady = false,
    llmEnabled = false
}));

app.Run();
