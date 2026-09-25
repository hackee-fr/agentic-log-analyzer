using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Application.Abstractions;

public interface ILogParser
{
    bool CanParse(RawLog rawLog);

    CanonicalEvent Parse(RawLog rawLog);
}
