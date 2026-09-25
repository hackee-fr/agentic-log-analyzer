using Microsoft.Extensions.DependencyInjection;

namespace AgenticLogAnalyzer.Detection;

public static class DependencyInjection
{
    public static IServiceCollection AddDetection(
        this IServiceCollection services)
    {
        return services;
    }
}
