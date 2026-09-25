using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Application.Parsing;
using AgenticLogAnalyzer.Connectors;
using AgenticLogAnalyzer.Infrastructure;
using AgenticLogAnalyzer.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<ILogParser, CanonicalEventParser>();
var inputPath = builder.Configuration["Ingestion:FilePath"];
builder.Services.AddSingleton<ILogConnector>(_ => string.IsNullOrWhiteSpace(inputPath)
    ? new SampleLogConnector()
    : new FileConnector(inputPath));
var storageProvider = builder.Configuration["Storage:Provider"] ?? "sqlite";
var sqlitePath = builder.Configuration["Storage:SqlitePath"]
    ?? DevelopmentStoragePaths.GetDatabasePath();
var legacyJsonLinesPaths = DevelopmentStoragePaths.GetLegacyJsonLinesPaths();
var jsonLinesPath = builder.Configuration["Storage:FilePath"]
    ?? Path.Combine(Directory.GetCurrentDirectory(), "data", "events.jsonl");
builder.Services.AddSingleton<IEventRepository>(_ => storageProvider.ToLowerInvariant() switch
{
    "sqlite" => new SqliteEventRepository(sqlitePath, legacyJsonLinesPaths),
    "jsonl" => new JsonLinesEventRepository(jsonLinesPath),
    _ => throw new InvalidOperationException($"Unsupported storage provider '{storageProvider}'. Use 'sqlite' or 'jsonl'.")
});
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
