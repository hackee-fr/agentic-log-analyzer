namespace AgenticLogAnalyzer.Application.Abstractions;

/// <summary>Live availability of the configured LLM provider.</summary>
/// <param name="Reachable">The provider answered the status request.</param>
/// <param name="ModelAvailable">The configured model is installed and can be used.</param>
/// <param name="InstalledModels">Models reported by the provider.</param>
/// <param name="Error">Why the provider is unavailable, when it is.</param>
public sealed record LlmStatus(
    string Provider,
    string Model,
    bool Reachable,
    bool ModelAvailable,
    IReadOnlyCollection<string> InstalledModels,
    string? Error);

public interface ILlmStatusProvider
{
    Task<LlmStatus> GetStatusAsync(CancellationToken cancellationToken);
}
