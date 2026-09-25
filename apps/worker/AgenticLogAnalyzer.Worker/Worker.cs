using AgenticLogAnalyzer.Application.Parsing;
using AgenticLogAnalyzer.Connectors;
using Microsoft.Extensions.Logging;

namespace AgenticLogAnalyzer.Worker;

public sealed partial class Worker(
    ILogger<Worker> logger,
    CanonicalEventParser parser) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        LogWorkerStarted(logger);

        var connector = new SampleLogConnector();

        await foreach (var rawLog in connector.ReadAsync(stoppingToken))
        {
            var canonicalEvent = parser.Parse(rawLog);

            LogCanonicalEvent(
                logger,
                canonicalEvent.Id,
                canonicalEvent.Category,
                canonicalEvent.Action,
                canonicalEvent.Result,
                canonicalEvent.User,
                canonicalEvent.Device);
        }

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Agentic Log Analyzer worker started.")]
    private static partial void LogWorkerStarted(
        ILogger logger);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Event {EventId}: {Category}/{Action} result={Result} user={User} device={Device}")]
    private static partial void LogCanonicalEvent(
        ILogger logger,
        Guid eventId,
        string category,
        string action,
        string? result,
        string? user,
        string? device);
}
