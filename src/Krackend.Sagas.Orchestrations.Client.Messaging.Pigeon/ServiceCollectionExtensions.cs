namespace Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon;

using Krackend.Sagas.Orchestrations.Client.DependencyInjection;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using global::Pigeon.Messaging.Consuming.Dispatching;
using global::Pigeon.Messaging.Producing;

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

        RegisterPigeonClientServices(builder.Services);

        return builder;
    }

    /// <summary>
    /// Adds Pigeon orchestration client publishing and configures Pigeon with application settings.
    /// </summary>
    public static KrackendOrchestrationsClientBuilder AddPigeon(
        this KrackendOrchestrationsClientBuilder builder,
        IConfiguration configuration,
        Action<GlobalSettingsBuilder> configure)
        => builder.AddPigeon(configuration, configure, _ => { });

    /// <summary>
    /// Adds Pigeon orchestration client publishing and configures Pigeon with application settings.
    /// </summary>
    public static KrackendOrchestrationsClientBuilder AddPigeon(
        this KrackendOrchestrationsClientBuilder builder,
        IConfiguration configuration,
        Action<GlobalSettingsBuilder> configure,
        Action<IPigeonServiceBuilder> configurePigeon)
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

        if (configurePigeon is null)
        {
            throw new ArgumentNullException(nameof(configurePigeon));
        }

        RegisterPigeonClientServices(builder.Services);
        var pigeonBuilder = builder.Services.AddPigeon(configuration, configure);
        configurePigeon(pigeonBuilder);

        return builder;
    }

    private static void RegisterPigeonClientServices(IServiceCollection services)
    {
        services.TryAddScoped<IMessagingReplyAddressSettingsSerializer, DefaultMessagingReplyAddressSettingsSerializer>();
        services.Replace(ServiceDescriptor.Scoped<IOrchestrationClientPublisher, PigeonOrchestrationClientPublisher>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IConsumeInterceptor, KrackendClientConsumeInterceptor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IConsumeExecutionInterceptor, KrackendClientConsumeInterceptor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPublishInterceptor, KrackendClientPublishInterceptor>());
    }
}
