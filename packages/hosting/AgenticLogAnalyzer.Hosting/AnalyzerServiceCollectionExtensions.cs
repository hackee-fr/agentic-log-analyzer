using AgenticLogAnalyzer.Agentic;
using AgenticLogAnalyzer.Agentic.Chat;
using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Application.Parsing;
using AgenticLogAnalyzer.Correlation;
using AgenticLogAnalyzer.Detection;
using AgenticLogAnalyzer.Infrastructure;
using AgenticLogAnalyzer.Llm;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticLogAnalyzer.Hosting;

public static class AnalyzerServiceCollectionExtensions
{
    /// <summary>Registers storage, parsing, detection, investigation, chat and the optional Ollama provider.</summary>
    public static IServiceCollection AddLogAnalyzer(this IServiceCollection services, AnalyzerHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddSingleton<ILogParser, CanonicalEventParser>();
        services.AddSingleton<IEventRepository>(_ => options.StorageProvider.ToLowerInvariant() switch
        {
            "sqlite" => new SqliteEventRepository(options.SqlitePath, options.LegacyJsonLinesPaths),
            "jsonl" => new JsonLinesEventRepository(options.JsonLinesPath),
            _ => throw new InvalidOperationException($"Unsupported storage provider '{options.StorageProvider}'. Use 'sqlite' or 'jsonl'.")
        });
        services.AddSingleton<IDetectionRule, RepeatedAuthenticationFailureRule>();
        services.AddSingleton<EventCorrelationService>();
        services.AddSingleton<SearchEventsTool>();
        services.AddSingleton<RunDetectionsTool>();
        services.AddSingleton<CorrelateEventsTool>();
        services.AddSingleton<InvestigationAgent>();
        services.AddSingleton<DetectionAgent>();
        services.AddSingleton<CorrelationAgent>();
        services.AddSingleton<ReportingAgent>();
        services.AddSingleton<InvestigationOrchestrator>();
        services.AddScoped<LogChatService>();

        if (options.LlmEnabled)
        {
            services.AddSingleton(new OllamaOptions(
                options.OllamaBaseUrl,
                options.OllamaModel,
                options.OllamaKeepAlive,
                options.OllamaMaxOutputTokens));
            services.AddHttpClient<OllamaLlmProvider>(client => client.Timeout = options.OllamaTimeout);
            services.AddTransient<ILlmProvider>(provider => provider.GetRequiredService<OllamaLlmProvider>());
            services.AddTransient<ILlmStatusProvider>(provider => provider.GetRequiredService<OllamaLlmProvider>());
        }

        return services;
    }
}
