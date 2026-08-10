using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Krackend.Sagas.Orchestrations.Messaging.Abstractions;

/// <summary>
/// Registers broker-neutral runtime messaging infrastructure.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the scoped orchestration metadata context used by messaging publishers and consumers.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddKrackendSagasOrchestrationsMessaging(this IServiceCollection services)
    {
        services.TryAddScoped<OrchestratorMetadataContext>();
        services.TryAddScoped<IOrchestratorMetadataAccessor>(provider =>
            provider.GetRequiredService<OrchestratorMetadataContext>());
        services.TryAddScoped<IOrchestratorMetadataWriter>(provider =>
            provider.GetRequiredService<OrchestratorMetadataContext>());

        return services;
    }
}
