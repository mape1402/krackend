namespace Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon;

using Krackend.Sagas.Orchestrations.Client.DependencyInjection;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registers Pigeon transport support for the Krackend orchestration client.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Pigeon orchestration client publishing and metadata consumption.
    /// </summary>
    public static KrackendOrchestrationsClientBuilder AddPigeon(this KrackendOrchestrationsClientBuilder builder)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.Services.Replace(ServiceDescriptor.Scoped<IOrchestrationClientPublisher, PigeonOrchestrationClientPublisher>());

        return builder;
    }

    /// <summary>
    /// Adds Pigeon orchestration client publishing and configures Pigeon with application settings.
    /// </summary>
    public static KrackendOrchestrationsClientBuilder AddPigeon(
        this KrackendOrchestrationsClientBuilder builder,
        IConfiguration configuration,
        Action<GlobalSettingsBuilder> configure)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        builder.Services.Replace(ServiceDescriptor.Scoped<IOrchestrationClientPublisher, PigeonOrchestrationClientPublisher>());
        builder.Services.AddPigeon(configuration, configure)
            .AddConsumeInterceptor<KrackendClientConsumeInterceptor>();

        return builder;
    }
}
