namespace Microsoft.Extensions.DependencyInjection;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.DependencyInjection;
using Krackend.Sagas.Orchestrations.Client.Metadata;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Krackend.Sagas.Orchestrations.Client.Responses;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registers Krackend orchestration client services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Krackend orchestration client defaults.
    /// </summary>
    public static KrackendOrchestrationsClientBuilder AddKrackendOrchestrationsClient(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddScoped<DefaultInstanceMetadataAccessor>();
        services.TryAddScoped<IInstanceMetadataAccessor>(provider =>
            provider.GetRequiredService<DefaultInstanceMetadataAccessor>());
        services.TryAddScoped<IInstanceMetadataSetter>(provider =>
            provider.GetRequiredService<DefaultInstanceMetadataAccessor>());
        services.TryAddScoped<IOrchestrationClientPublisher, DefaultOrchestrationClientPublisher>();
        services.TryAddScoped<IOrchestrationClientResponseFactory, DefaultOrchestrationClientResponseFactory>();

        return new KrackendOrchestrationsClientBuilder(services);
    }
}
