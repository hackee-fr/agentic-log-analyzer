using Microsoft.Extensions.DependencyInjection;

namespace AgenticLogAnalyzer.Correlation;

public static class DependencyInjection
{
    public static IServiceCollection AddCorrelation(
        this IServiceCollection services)
    {
        return services;
    }
}
