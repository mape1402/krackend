using Krackend.Sagas.Orchestrations.Client;
using Krackend.Sagas.Orchestrations.Client.Abstractions;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Krackend.Sagas.Orchestrations.Client.Pigeon;

/// <summary>
/// Dependency injection extensions for the Pigeon orchestration client adapter.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds orchestration client services backed by Pigeon metadata and publishing.
    /// </summary>
    public static IServiceCollection AddKrackendSagasOrchestrationsClientPigeon(this IServiceCollection services)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        services.AddKrackendSagasOrchestrationsMessaging();
        services.AddKrackendSagasOrchestrationsClient();

        services.Replace(ServiceDescriptor.Scoped<IOrchestrationClientMetadataReader, PigeonOrchestrationClientMetadataReader>());
        services.Replace(ServiceDescriptor.Scoped<IOrchestrationOutputPublisher, PigeonOrchestrationOutputPublisher>());

        return services;
    }
}
