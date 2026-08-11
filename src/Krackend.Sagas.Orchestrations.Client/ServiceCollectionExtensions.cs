using Krackend.Sagas.Orchestrations.Client.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Client;

/// <summary>
/// Dependency injection extensions for orchestration client services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds broker-agnostic orchestration client services.
    /// </summary>
    public static IServiceCollection AddKrackendSagasOrchestrationsClient(this IServiceCollection services)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        services.AddScoped<OrchestrationClientMetadataContext>();
        services.AddScoped<IOrchestrationClientMetadataAccessor>(provider => provider.GetRequiredService<OrchestrationClientMetadataContext>());
        services.AddScoped<IOrchestrationClientMetadataWriter>(provider => provider.GetRequiredService<OrchestrationClientMetadataContext>());
        services.AddSingleton<IOrchestrationClientClock, SystemOrchestrationClientClock>();
        services.AddSingleton<IOrchestrationOutputPayloadFactory, OrchestrationOutputPayloadFactory>();
        services.AddScoped<IOrchestrationClientExecutionCoordinator, OrchestrationClientExecutionCoordinator>();

        return services;
    }
}
