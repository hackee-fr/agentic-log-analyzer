namespace AgenticLogAnalyzer.Domain.Logs;

public sealed record RawLog(
    string Source,
    string Content,
    DateTimeOffset ReceivedAt);
