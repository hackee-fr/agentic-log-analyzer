namespace AgenticLogAnalyzer.Application.Abstractions;

public interface ILlmProvider
{
    Task<string> GenerateAsync(
        string systemPrompt,
        string prompt,
        CancellationToken cancellationToken);
}
