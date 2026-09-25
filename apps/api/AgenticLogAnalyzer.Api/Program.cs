using AgenticLogAnalyzer.Hosting;
using AgenticLogAnalyzer.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var options = AnalyzerHostOptions.FromConfiguration(
    builder.Configuration,
    DevelopmentStoragePaths.GetDatabasePath(),
    DevelopmentStoragePaths.GetLegacyJsonLinesPaths(),
    Path.Combine(Directory.GetCurrentDirectory(), "data", "events.jsonl"));
var dashboardOrigins = new[]
{
    builder.Configuration["Dashboard:Origin"] ?? "http://localhost:5081",
    builder.Configuration["Dashboard:DevelopmentOrigin"] ?? "http://localhost:5173"
};

builder.Services.AddLogAnalyzer(options);
builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy =>
    policy.WithOrigins(dashboardOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
app.MapLogAnalyzerApi();
app.Run();
