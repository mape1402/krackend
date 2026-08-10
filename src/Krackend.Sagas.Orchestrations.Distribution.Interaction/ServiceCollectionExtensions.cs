using Microsoft.Extensions.DependencyInjection;
using Krackend.Sagas.Orchestrations.Contracts.Eventing;
using Krackend.Sagas.Orchestrations.Contracts.Events;

namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrchestratorDistributionInteraction(this IServiceCollection services)
    {
        services.AddScoped<IRuntimeEnvironmentInteractionService, RuntimeEnvironmentInteractionService>();
        services.AddScoped<IRuntimeNodeInteractionService, RuntimeNodeInteractionService>();
        services.AddScoped<IArtifactInteractionService, ArtifactInteractionService>();
        services.AddScoped<IReleaseInteractionService, ReleaseInteractionService>();
        services.AddScoped<IReleaseTargetInteractionService, ReleaseTargetInteractionService>();
        services.AddHttpClient<IArtifactDeliveryInteractionService, ArtifactDeliveryInteractionService>();
        services.AddScoped<IOrchestrationNodePolicyInteractionService, OrchestrationNodePolicyInteractionService>();
        services.AddScoped<IArtifactValidationPolicy, JsonArtifactValidationPolicy>();
        services.AddScoped<IArtifactBuilder<OrchestrationVersionDeployedEvent>, DeployedArtifactBuilder>();
        services.AddScoped<IArtifactBuilder<OrchestrationVersionDeprecatedEvent>, DeprecatedArtifactBuilder>();
        services.AddScoped<IArtifactBuilder<OrchestrationVersionArchivedEvent>, ArchivedArtifactBuilder>();
        services.AddScoped<IIntegrationEventHandler<OrchestrationVersionDeployedEvent>, OrchestrationVersionDeployedEventHandler>();
        services.AddScoped<IIntegrationEventHandler<OrchestrationVersionDeprecatedEvent>, OrchestrationVersionDeprecatedEventHandler>();
        services.AddScoped<IIntegrationEventHandler<OrchestrationVersionArchivedEvent>, OrchestrationVersionArchivedEventHandler>();
        services.AddScoped<IIntegrationEventHandler<OrchestrationDefinitionCreatedEvent>, OrchestrationDefinitionCreatedEventHandler>();
        services.AddScoped<IIntegrationEventHandler<OrchestrationDefinitionUpdatedEvent>, OrchestrationDefinitionUpdatedEventHandler>();
        services.AddScoped<IIntegrationEventHandler<OrchestrationDefinitionDeactivatedEvent>, OrchestrationDefinitionDeactivatedEventHandler>();
        return services;
    }
}


