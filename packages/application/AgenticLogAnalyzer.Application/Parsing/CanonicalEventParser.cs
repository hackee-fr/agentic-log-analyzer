using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Application.Parsing;

public sealed partial class CanonicalEventParser : ILogParser
{
    private const int FieldCount = 7;
    private static readonly HashSet<string> Levels = new(StringComparer.OrdinalIgnoreCase)
    {
        "TRACE", "DEBUG", "INFO", "WARN", "WARNING", "ERROR", "FATAL", "CRITICAL"
    };

    public bool CanParse(RawLog rawLog)
    {
        return Split(rawLog).Length == FieldCount || TrySplitStructured(rawLog.Content, out _);
    }

    /// <exception cref="FormatException">
    /// The line is not a supported format or its timestamp is invalid.
    /// </exception>
    public CanonicalEvent Parse(RawLog rawLog)
    {
        var parts = Split(rawLog);

        if (parts.Length != FieldCount)
        {
            return ParseStructured(rawLog);
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
            CanonicalEventId.Create(rawLog, timestamp),
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

    private static CanonicalEvent ParseStructured(RawLog rawLog)
    {
        if (!TrySplitStructured(rawLog.Content, out var fields))
        {
            throw new FormatException(
                "Expected 7 pipe-delimited fields or 'timestamp LEVEL [component] message'.");
        }

        if (!DateTimeOffset.TryParse(
                fields.Timestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var timestamp))
        {
            throw new FormatException($"Invalid timestamp: '{fields.Timestamp}'.");
        }

        var attributes = AttributeRegex().Matches(fields.Message)
            .ToDictionary(
                match => match.Groups["key"].Value.Replace("\\_", "_", StringComparison.Ordinal).ToLowerInvariant(),
                match => Unquote(match.Groups["value"].Value),
                StringComparer.OrdinalIgnoreCase);
        var action = ExtractAction(fields.Message);
        var result = GetResult(fields.Level, fields.Message);
        var user = FindAttribute(attributes, "user", "user_id", "username", "account");
        var device = FindAttribute(attributes, "device", "host", "hostname", "container");
        var source = FindAttribute(attributes, "sourceip", "source_ip", "src_ip", "source");
        var sourceIp = IPAddress.TryParse(source, out _) ? source : null;

        return new CanonicalEvent(
            CanonicalEventId.Create(rawLog, timestamp),
            timestamp,
            rawLog.Source,
            rawLog.SourceName,
            fields.Category,
            action,
            result,
            user,
            device,
            sourceIp,
            rawLog.Content);
    }

    private static bool TrySplitStructured(string content, out StructuredFields fields)
    {
        var parts = content.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 4
            && Levels.Contains(parts[1])
            && parts[2].Length > 2
            && parts[2][0] == '['
            && parts[2][^1] == ']')
        {
            fields = new StructuredFields(parts[0], parts[1], parts[2][1..^1], parts[3]);
            return true;
        }

        fields = default;
        return false;
    }

    private static string ExtractAction(string message)
    {
        var assignment = AttributeRegex().Match(message);
        var actionText = assignment.Success ? message[..assignment.Index] : message;
        var action = string.Join(' ', actionText.Trim().TrimEnd(',', ';').Split(
            ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5));
        return string.IsNullOrWhiteSpace(action) ? "event" : action;
    }

    private static string GetResult(string level, string message)
    {
        if (message.Contains("fail", StringComparison.OrdinalIgnoreCase)
            || message.Contains("locked", StringComparison.OrdinalIgnoreCase))
        {
            return "failure";
        }

        if (message.Contains("success", StringComparison.OrdinalIgnoreCase)
            || message.Contains("passed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("completed", StringComparison.OrdinalIgnoreCase))
        {
            return "success";
        }

        return level.ToLowerInvariant() switch
        {
            "error" or "fatal" or "critical" => "failure",
            "warn" or "warning" => "warning",
            _ => level.ToLowerInvariant()
        };
    }

    private static string? FindAttribute(Dictionary<string, string> attributes, params string[] names)
    {
        foreach (var name in names)
        {
            if (attributes.TryGetValue(name, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal)
            : value;

    [GeneratedRegex("(?<key>[\\w\\\\]+)=(?<value>\"(?:\\\\.|[^\"])*\"|\\S+)", RegexOptions.CultureInvariant)]
    private static partial Regex AttributeRegex();

    private static string[] Split(RawLog rawLog) =>
        rawLog.Content.Split('|', StringSplitOptions.TrimEntries);

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private readonly record struct StructuredFields(string Timestamp, string Level, string Category, string Message);
}
