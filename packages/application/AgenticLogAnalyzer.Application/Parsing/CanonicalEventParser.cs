using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Application.Parsing;

public sealed class CanonicalEventParser
{
    public CanonicalEvent Parse(RawLog rawLog)
    {
        var parts = rawLog.Content.Split(
            '|',
            StringSplitOptions.TrimEntries);

        if (parts.Length != 7)
        {
            throw new FormatException(
                "Expected 7 pipe-delimited fields: timestamp|category|action|result|user|device|sourceIp.");
        }

        if (!DateTimeOffset.TryParse(parts[0], out var timestamp))
        {
            throw new FormatException($"Invalid timestamp: '{parts[0]}'.");
        }

        return new CanonicalEvent(
            Guid.NewGuid(),
            timestamp,
            rawLog.Source,
            rawLog.Source,
            parts[1],
            parts[2],
            NullIfEmpty(parts[3]),
            NullIfEmpty(parts[4]),
            NullIfEmpty(parts[5]),
            NullIfEmpty(parts[6]),
            rawLog.Content);
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
