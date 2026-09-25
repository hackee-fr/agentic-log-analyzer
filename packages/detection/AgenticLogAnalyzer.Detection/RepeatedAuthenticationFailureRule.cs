using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Detection;

public sealed record DetectionFinding(
    string RuleId,
    string Title,
    string Severity,
    string Description,
    IReadOnlyCollection<Guid> EvidenceEventIds,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen,
    string? User,
    string? SourceIp);

public interface IDetectionRule
{
    IReadOnlyCollection<DetectionFinding> Evaluate(IEnumerable<CanonicalEvent> events);
}

/// <summary>Finds three or more failed authentication events for the same identity and IP in ten minutes.</summary>
public sealed class RepeatedAuthenticationFailureRule : IDetectionRule
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
    private const int Threshold = 3;

    public IReadOnlyCollection<DetectionFinding> Evaluate(IEnumerable<CanonicalEvent> events)
    {
        var failures = events
            .Where(IsAuthenticationFailure)
            .OrderBy(item => item.Timestamp)
            .GroupBy(item => (item.User, item.SourceIp));

        var findings = new List<DetectionFinding>();
        foreach (var group in failures)
        {
            var ordered = group.ToArray();
            for (var start = 0; start <= ordered.Length - Threshold; start++)
            {
                var end = start + Threshold - 1;
                while (end + 1 < ordered.Length && ordered[end + 1].Timestamp - ordered[start].Timestamp <= Window)
                {
                    end++;
                }

                if (end - start + 1 < Threshold)
                {
                    continue;
                }

                var evidence = ordered[start..(end + 1)];
                findings.Add(new DetectionFinding(
                    "AUTH-001",
                    "Repeated authentication failures",
                    "high",
                    $"{evidence.Length} failed authentication events for {group.Key.User ?? "unknown user"} from {group.Key.SourceIp ?? "unknown IP"} within ten minutes.",
                    evidence.Select(item => item.Id).ToArray(),
                    evidence[0].Timestamp,
                    evidence[^1].Timestamp,
                    group.Key.User,
                    group.Key.SourceIp));
                break;
            }
        }

        return findings;
    }

    private static bool IsAuthenticationFailure(CanonicalEvent item) =>
        string.Equals(item.Result, "failure", StringComparison.OrdinalIgnoreCase)
        && (item.User is not null || item.SourceIp is not null)
        && (item.Category.Contains("auth", StringComparison.OrdinalIgnoreCase)
            || item.Action.Contains("auth", StringComparison.OrdinalIgnoreCase)
            || item.Action.Contains("login", StringComparison.OrdinalIgnoreCase)
            || item.Action.Contains("logon", StringComparison.OrdinalIgnoreCase));
}
