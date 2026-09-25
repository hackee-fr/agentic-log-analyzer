using AgenticLogAnalyzer.Application.Parsing;
using AgenticLogAnalyzer.Connectors;

namespace AgenticLogAnalyzer.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    CanonicalEventParser parser) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation("Agentic Log Analyzer worker started.");

        var connector = new SampleLogConnector();

        await foreach (var rawLog in connector.ReadAsync(stoppingToken))
        {
            var canonicalEvent = parser.Parse(rawLog);

            logger.LogInformation(
                "Event {EventId}: {Category}/{Action} result={Result} user={User} device={Device}",
                canonicalEvent.Id,
                canonicalEvent.Category,
                canonicalEvent.Action,
                canonicalEvent.Result,
                canonicalEvent.User,
                canonicalEvent.Device);
        }

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
