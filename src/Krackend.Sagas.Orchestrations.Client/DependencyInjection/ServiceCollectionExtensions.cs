namespace Microsoft.Extensions.DependencyInjection;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.DependencyInjection;
using Krackend.Sagas.Orchestrations.Client.Metadata;
using Krackend.Sagas.Orchestrations.Client.Publishing;
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

        services.TryAddScoped<DefaultOrchestrationMessageMetadataAccessor>();
        services.TryAddScoped<IOrchestrationMessageMetadataAccessor>(provider =>
            provider.GetRequiredService<DefaultOrchestrationMessageMetadataAccessor>());
        services.TryAddScoped<IOrchestrationMessageMetadataSetter>(provider =>
            provider.GetRequiredService<DefaultOrchestrationMessageMetadataAccessor>());
        services.TryAddScoped<DefaultOrchestrationExecutionResultMetadataAccessor>();
        services.TryAddScoped<IOrchestrationExecutionResultMetadataAccessor>(provider =>
            provider.GetRequiredService<DefaultOrchestrationExecutionResultMetadataAccessor>());
        services.TryAddScoped<IOrchestrationExecutionResultMetadataSetter>(provider =>
            provider.GetRequiredService<DefaultOrchestrationExecutionResultMetadataAccessor>());
        services.TryAddScoped<IOrchestrationClientPublisher, DefaultOrchestrationClientPublisher>();
        services.TryAddScoped<IOrchestrationOperationExecutionContext, DefaultOrchestrationOperationExecutionContext>();
        services.TryAddScoped<IOrchestrationExecutionResultMetadataFactory, DefaultOrchestrationExecutionResultMetadataFactory>();
        services.TryAddScoped<IOrchestrationPipelinePublisher, DefaultOrchestrationPipelinePublisher>();

        return new KrackendOrchestrationsClientBuilder(services);
    }
}
