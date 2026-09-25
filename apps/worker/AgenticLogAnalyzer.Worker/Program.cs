using AgenticLogAnalyzer.Application.Parsing;
using AgenticLogAnalyzer.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<CanonicalEventParser>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
