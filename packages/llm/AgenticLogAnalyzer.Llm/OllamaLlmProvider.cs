using System.Net.Http.Json;
using System.Text.Json;
using AgenticLogAnalyzer.Application.Abstractions;

namespace AgenticLogAnalyzer.Llm;

public sealed record OllamaOptions(Uri BaseAddress, string Model);

/// <summary>Calls a local Ollama server through its non-streaming <c>/api/chat</c> endpoint.</summary>
public sealed class OllamaLlmProvider(HttpClient httpClient, OllamaOptions options) : ILlmProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> GenerateAsync(
        string systemPrompt,
        string prompt,
        CancellationToken cancellationToken)
    {
        var request = new OllamaChatRequest(
            options.Model,
            [new OllamaMessage("system", systemPrompt), new OllamaMessage("user", prompt)],
            Stream: false,
            new OllamaGenerationOptions(Temperature: 0.1));

        using var response = await httpClient.PostAsJsonAsync(
            new Uri(options.BaseAddress, "api/chat"),
            request,
            JsonOptions,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Ollama returned HTTP {(int)response.StatusCode}: {body}");
        }

        try
        {
            var payload = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, cancellationToken);
            return payload?.Message?.Content
                ?? throw new InvalidOperationException("Ollama returned a response without message content.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Ollama returned an unreadable response.", exception);
        }
    }

    private sealed record OllamaChatRequest(
        string Model,
        IReadOnlyCollection<OllamaMessage> Messages,
        bool Stream,
        OllamaGenerationOptions Options);

    private sealed record OllamaGenerationOptions(double Temperature);

    private sealed record OllamaMessage(string Role, string Content);

    private sealed record OllamaChatResponse(OllamaMessage? Message);
}
