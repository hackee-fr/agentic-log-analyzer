using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Application.Parsing;
using AgenticLogAnalyzer.Connectors;
using AgenticLogAnalyzer.Infrastructure;
using AgenticLogAnalyzer.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<ILogParser, CanonicalEventParser>();
var inputPath = builder.Configuration["Ingestion:FilePath"];
var useSample = builder.Configuration.GetValue("Ingestion:UseSample", false);
var follow = builder.Configuration.GetValue("Ingestion:Follow", true);
var pollInterval = TimeSpan.FromMilliseconds(builder.Configuration.GetValue("Ingestion:PollIntervalMs", 1000));
if (!string.IsNullOrWhiteSpace(inputPath))
{
  builder.Services.AddSingleton<ILogConnector>(_ => follow
      ? new TailingFileConnector(inputPath, pollInterval)
      : new FileConnector(inputPath));
}
else if (useSample)
{
  builder.Services.AddSingleton<ILogConnector, SampleLogConnector>();
}
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
