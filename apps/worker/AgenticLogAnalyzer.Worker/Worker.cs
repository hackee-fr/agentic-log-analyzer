using AgenticLogAnalyzer.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace AgenticLogAnalyzer.Worker;

public sealed partial class Worker(
    ILogger<Worker> logger,
    ILogConnector connector,
    ILogParser parser,
    IEventRepository repository) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        LogWorkerStarted(logger);

        await foreach (var rawLog in connector.ReadAsync(stoppingToken))
        {
            if (!parser.CanParse(rawLog))
            {
                LogUnparseableLine(logger, rawLog.SourceName, "unexpected field count");
                continue;
            }

            try
            {
                var canonicalEvent = parser.Parse(rawLog);
                await repository.SaveAsync(canonicalEvent, stoppingToken);

                LogCanonicalEvent(
                    logger,
                    canonicalEvent.Id,
                    canonicalEvent.Category,
                    canonicalEvent.Action,
                    canonicalEvent.Result,
                    canonicalEvent.User,
                    canonicalEvent.Device);
            }
            catch (FormatException ex)
            {
                LogUnparseableLine(logger, rawLog.SourceName, ex.Message);
            }
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

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Skipped unparseable line from {SourceName}: {Reason}")]
    private static partial void LogUnparseableLine(
        ILogger logger,
        string sourceName,
        string reason);
}
