using AgenticLogAnalyzer.Detection;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Agentic.Chat;

public sealed record ChatRequest(string? Question);

public enum ChatIntent
{
    Summary,
    Security,
    TopSourceIps,
    TopUsers,
    Timeline,
    List
}

/// <summary>Deterministic interpretation of a natural-language question.</summary>
public sealed record ChatQuery(ChatIntent Intent, string? ResultFilter, IReadOnlyCollection<string> Terms);

/// <summary>
/// Chat answer. <see cref="DeterministicAnswer"/> and <see cref="Evidence"/> are always computed from stored
/// events; <see cref="Answer"/> is the LLM rewording when <see cref="LlmUsed"/> is true.
/// </summary>
public sealed record ChatAnswer(
    string Question,
    string Intent,
    IReadOnlyCollection<string> AppliedFilters,
    int MatchedEventCount,
    string Answer,
    string DeterministicAnswer,
    IReadOnlyCollection<CanonicalEvent> Evidence,
    IReadOnlyCollection<DetectionFinding> Detections,
    bool LlmUsed,
    string? LlmError);
