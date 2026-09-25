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
var dataFile = builder.Configuration["Storage:FilePath"]
    ?? Path.Combine(Directory.GetCurrentDirectory(), "data", "events.jsonl");
builder.Services.AddSingleton<IEventRepository>(_ => new JsonLinesEventRepository(dataFile));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
