using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Application.Parsing;
using AgenticLogAnalyzer.Connectors;
using AgenticLogAnalyzer.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<ILogParser, CanonicalEventParser>();
builder.Services.AddSingleton<ILogConnector, SampleLogConnector>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
