using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Registers runtime interaction services.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKrackendSagasOrchestrationsWeb(
        this IServiceCollection services,
        Action<RuntimeArtifactPullOptions> configureArtifactPull = null)
    {
        if (configureArtifactPull is not null)
        {
            services.Configure(configureArtifactPull);
        }

        services.AddOptions<RuntimeIngressSynchronizationOptions>();
        services.AddScoped<IRuntimeArtifactDeploymentService, RuntimeArtifactDeploymentService>();
        services.AddScoped<IRuntimeArtifactIngressBindingBuilder, RuntimeArtifactIngressBindingBuilder>();
        services.AddScoped<IRuntimeIngressConnector, MessageIngressConnector>();
        services.AddScoped<IRuntimeArtifactConsumerSynchronizer, RuntimeArtifactConsumerSynchronizer>();
        services.AddScoped<IRuntimeIngressSynchronizer>(provider => provider.GetRequiredService<IRuntimeArtifactConsumerSynchronizer>() as IRuntimeIngressSynchronizer);
        services.AddScoped<IRuntimeBackChannelResponseHandler, RuntimeBackChannelResponseHandler>();
        services.AddScoped<IRuntimeTriggerInteractionService, RuntimeTriggerInteractionService>();
        services.AddHttpClient<IRuntimeArtifactPullService, RuntimeArtifactPullService>();
        services.AddHostedService<RuntimeActiveArtifactConsumerHostedService>();
        return services;
    }
}
