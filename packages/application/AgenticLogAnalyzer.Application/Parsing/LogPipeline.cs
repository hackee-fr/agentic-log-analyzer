using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Application.Parsing;

public sealed class LogPipeline(
    ILogParser parser)
{
    public IReadOnlyCollection<CanonicalEvent> Process(
        IEnumerable<RawLog> rawLogs)
    {
        return rawLogs
            .Select(parser.Parse)
            .ToArray();
    }
}
