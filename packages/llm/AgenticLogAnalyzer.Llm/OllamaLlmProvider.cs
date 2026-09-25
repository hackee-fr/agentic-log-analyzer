using AgenticLogAnalyzer.Application.Abstractions;

namespace AgenticLogAnalyzer.Llm;

public sealed class OllamaLlmProvider : ILlmProvider
{
    public Task<string> GenerateAsync(
        string systemPrompt,
        string prompt,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException(
            "Ollama integration is intentionally deferred until the deterministic log engine is ready.");
    }
}
