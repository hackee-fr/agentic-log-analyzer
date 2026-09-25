using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticLogAnalyzer.Application.Abstractions;

namespace AgenticLogAnalyzer.Llm;

/// <param name="KeepAlive">How long Ollama keeps the model loaded after a request (Ollama duration, e.g. "15m").</param>
/// <param name="MaxOutputTokens">Upper bound on generated tokens; keeps CPU-only inference responsive.</param>
public sealed record OllamaOptions(Uri BaseAddress, string Model, string KeepAlive = "15m", int MaxOutputTokens = 350);

/// <summary>Calls a local Ollama server through its non-streaming <c>/api/chat</c> endpoint.</summary>
public sealed class OllamaLlmProvider(HttpClient httpClient, OllamaOptions options) : ILlmProvider, ILlmStatusProvider
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
            options.KeepAlive,
            new OllamaGenerationOptions(Temperature: 0.1, options.MaxOutputTokens));

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

    /// <summary>Lists installed models through <c>/api/tags</c>; never throws for an unreachable server.</summary>
    public async Task<LlmStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(new Uri(options.BaseAddress, "api/tags"), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Unavailable($"Ollama returned HTTP {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(JsonOptions, cancellationToken);
            var models = payload?.Models?.Select(model => model.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray() ?? [];
            var available = models.Any(IsConfiguredModel);
            return new LlmStatus(
                "Ollama",
                options.Model,
                Reachable: true,
                available,
                models,
                available ? null : $"Model '{options.Model}' is not installed. Run: ollama pull {options.Model}");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return Unavailable($"Ollama is unreachable at {options.BaseAddress}: {exception.Message}");
        }
    }

    // "llama3.2" matches the "llama3.2:latest" tag reported by Ollama.
    private bool IsConfiguredModel(string name) =>
        string.Equals(name, options.Model, StringComparison.OrdinalIgnoreCase)
        || (!options.Model.Contains(':', StringComparison.Ordinal)
            && string.Equals(name, $"{options.Model}:latest", StringComparison.OrdinalIgnoreCase));

    private LlmStatus Unavailable(string error) =>
        new("Ollama", options.Model, Reachable: false, ModelAvailable: false, [], error);

    private sealed record OllamaChatRequest(
        string Model,
        IReadOnlyCollection<OllamaMessage> Messages,
        bool Stream,
        [property: JsonPropertyName("keep_alive")] string KeepAlive,
        OllamaGenerationOptions Options);

    private sealed record OllamaGenerationOptions(
        double Temperature,
        [property: JsonPropertyName("num_predict")] int NumPredict);

    private sealed record OllamaMessage(string Role, string Content);

    private sealed record OllamaChatResponse(OllamaMessage? Message);

    private sealed record OllamaTagsResponse(IReadOnlyCollection<OllamaModel>? Models);

    private sealed record OllamaModel(string Name);
}
