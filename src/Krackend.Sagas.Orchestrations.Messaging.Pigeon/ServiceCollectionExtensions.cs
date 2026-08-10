using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Consuming;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Publishing;

namespace Krackend.Sagas.Orchestrations.Messaging.Pigeon;

/// <summary>
/// Registers the Pigeon-backed runtime messaging adapter.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Pigeon as the concrete messaging adapter for the runtime facade.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Application configuration used by Pigeon.</param>
    /// <param name="configurePigeon">Pigeon configuration callback, including broker adapter registration.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddKrackendSagasOrchestrationsMessagingPigeon(this IServiceCollection services, IConfiguration configuration, Action<GlobalSettingsBuilder> configurePigeon)
    {
        if (configuration is null)
            throw new ArgumentNullException(nameof(configuration));

        if (configurePigeon is null)
            throw new ArgumentNullException(nameof(configurePigeon));

        services.AddKrackendSagasOrchestrationsMessaging();

        services.AddPigeon(configuration, configurePigeon)
            .AddPublishInterceptor<OrchestratorPigeonPublishMetadataInterceptor>()
            .AddConsumeInterceptor<OrchestratorPigeonConsumeMetadataInterceptor>();

        services.Replace(ServiceDescriptor.Scoped<IMessagePublisher, PigeonMessagePublisher>());
        services.TryAddScoped<IMessageConsumerRegistry, PigeonMessageConsumerRegistry>();

        return services;
    }
}
