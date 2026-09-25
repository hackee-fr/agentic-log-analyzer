namespace AgenticLogAnalyzer.Domain.Logs;

/// <param name="Attributes">
/// Every key=value pair found in the source line (keys lowercased), including the ones already mapped to
/// canonical fields; null when the format has none. Values are kept as text, exactly as logged.
/// </param>
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
    string RawContent,
    IReadOnlyDictionary<string, string>? Attributes = null);
