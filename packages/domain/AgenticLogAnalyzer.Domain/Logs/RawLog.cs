namespace AgenticLogAnalyzer.Domain.Logs;

/// <param name="Source">Connector type that produced the log (e.g. "file").</param>
/// <param name="SourceName">Concrete source instance (e.g. the file path).</param>
public sealed record RawLog(
    string Source,
    string SourceName,
    string Content,
    DateTimeOffset ReceivedAt);
