using System.Globalization;
using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Application.Parsing;

public sealed class CanonicalEventParser : ILogParser
{
    private const int FieldCount = 7;

    public bool CanParse(RawLog rawLog)
    {
        return Split(rawLog).Length == FieldCount;
    }

    /// <exception cref="FormatException">
    /// The line does not have exactly 7 fields or its timestamp is invalid.
    /// </exception>
    public CanonicalEvent Parse(RawLog rawLog)
    {
        var parts = Split(rawLog);

        if (parts.Length != FieldCount)
        {
            throw new FormatException(
                "Expected 7 pipe-delimited fields: timestamp|category|action|result|user|device|sourceIp.");
        }

        if (!DateTimeOffset.TryParse(
                parts[0],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var timestamp))
        {
            throw new FormatException($"Invalid timestamp: '{parts[0]}'.");
        }

        return new CanonicalEvent(
            Guid.CreateVersion7(timestamp),
            timestamp,
            rawLog.Source,
            rawLog.SourceName,
            parts[1],
            parts[2],
            NullIfEmpty(parts[3]),
            NullIfEmpty(parts[4]),
            NullIfEmpty(parts[5]),
            NullIfEmpty(parts[6]),
            rawLog.Content);
    }

    private static string[] Split(RawLog rawLog) =>
        rawLog.Content.Split('|', StringSplitOptions.TrimEntries);

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
