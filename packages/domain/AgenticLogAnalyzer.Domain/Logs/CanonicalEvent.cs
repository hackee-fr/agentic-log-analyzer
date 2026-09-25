namespace AgenticLogAnalyzer.Domain.Logs;

public sealed record CanonicalEvent(
    Guid Id,
    DateTimeOffset Timestamp,
    string SourceType,
    string SourceName,
    string Category,
    string Action,
    string? Result,
    string? User,
    string? Device,
    string? SourceIp,
    string RawContent);
