using Microsoft.Extensions.Configuration;

namespace AgenticLogAnalyzer.Hosting;

/// <summary>Resolved runtime settings shared by the web API and the desktop app.</summary>
public sealed record AnalyzerHostOptions(
    string StorageProvider,
    string SqlitePath,
    IReadOnlyCollection<string> LegacyJsonLinesPaths,
    string JsonLinesPath,
    bool LlmEnabled,
    Uri OllamaBaseUrl,
    string OllamaModel,
    string OllamaKeepAlive,
    int OllamaMaxOutputTokens,
    TimeSpan OllamaTimeout)
{
    public bool UsesSqlite => StorageProvider.Equals("sqlite", StringComparison.OrdinalIgnoreCase);

    public string StoragePath => UsesSqlite ? SqlitePath : JsonLinesPath;

    /// <summary>Reads settings from configuration; each host supplies its own defaults for what is not configured.</summary>
    public static AnalyzerHostOptions FromConfiguration(
        IConfiguration configuration,
        string defaultSqlitePath,
        IReadOnlyCollection<string> legacyJsonLinesPaths,
        string defaultJsonLinesPath,
        string? defaultLlmProvider = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var llmProvider = configuration["Llm:Provider"] ?? defaultLlmProvider;
        return new AnalyzerHostOptions(
            configuration["Storage:Provider"] ?? "sqlite",
            configuration["Storage:SqlitePath"] ?? defaultSqlitePath,
            legacyJsonLinesPaths,
            configuration["Storage:FilePath"] ?? defaultJsonLinesPath,
            string.Equals(llmProvider, "ollama", StringComparison.OrdinalIgnoreCase),
            new Uri(configuration["Llm:Ollama:BaseUrl"] ?? "http://localhost:11434"),
            configuration["Llm:Ollama:Model"] ?? "llama3.2",
            configuration["Llm:Ollama:KeepAlive"] ?? "15m",
            configuration.GetValue("Llm:Ollama:MaxOutputTokens", 350),
            // CPU inference in Docker can be slow, especially while the model loads on the first request.
            TimeSpan.FromSeconds(configuration.GetValue("Llm:Ollama:TimeoutSeconds", 180)));
    }
}
