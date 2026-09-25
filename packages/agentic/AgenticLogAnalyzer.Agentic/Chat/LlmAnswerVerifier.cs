using System.Text.RegularExpressions;

namespace AgenticLogAnalyzer.Agentic.Chat;

/// <summary>
/// Deterministic checks applied to an LLM rewording before it is shown. The LLM is never authoritative:
/// an answer that cites unknown IP addresses or denies a deterministic detection is rejected.
/// </summary>
public static partial class LlmAnswerVerifier
{
    public static IReadOnlyCollection<string> Verify(string llmAnswer, string facts, bool hasDetections)
    {
        ArgumentNullException.ThrowIfNull(llmAnswer);
        ArgumentNullException.ThrowIfNull(facts);

        var issues = new List<string>();
        var unknownIps = Ipv4Regex().Matches(llmAnswer)
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .Where(ip => !facts.Contains(ip, StringComparison.Ordinal))
            .ToArray();
        if (unknownIps.Length > 0)
        {
            issues.Add($"adresse(s) IP absente(s) des faits : {string.Join(", ", unknownIps)}");
        }

        if (hasDetections && DenialRegex().IsMatch(llmAnswer))
        {
            issues.Add("la réponse nie une menace alors qu'une détection déterministe s'est déclenchée");
        }

        return issues;
    }

    [GeneratedRegex(@"\b(?:25[0-5]|2[0-4]\d|1?\d?\d)(?:\.(?:25[0-5]|2[0-4]\d|1?\d?\d)){3}\b", RegexOptions.CultureInvariant)]
    private static partial Regex Ipv4Regex();

    [GeneratedRegex(
        @"\b(?:pas|aucune?|ni)\s+(?:d['’]\s*|de\s+)?(?:indication\s+d['’]\s*|signe\s+d['’]\s*)?(?:attaques?|menaces?|intrusions?|activit[ée]s?\s+suspectes?|incidents?)\b"
        + @"|\bno\s+(?:sign\s+of\s+)?(?:attacks?|threats?|intrusions?|suspicious\s+activity)\b"
        + @"|\bnot\s+an?\s+attack\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DenialRegex();
}
