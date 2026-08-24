using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Krackend.Sagas.Orchestrations.Contracts.Events;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrchestratorDistributionApplication(this IServiceCollection services)
    {
        services.AddScoped<IRuntimeEnvironmentApplicationService, RuntimeEnvironmentApplicationService>();
        services.AddScoped<IRuntimeNodeApplicationService, RuntimeNodeApplicationService>();
        services.AddScoped<IArtifactApplicationService, ArtifactApplicationService>();
        services.AddScoped<IReleaseApplicationService, ReleaseApplicationService>();
        services.AddScoped<IReleaseTargetApplicationService, ReleaseTargetApplicationService>();
        services.AddHttpClient<IArtifactDeliveryApplicationService, ArtifactDeliveryApplicationService>();
        services.AddOptions<ArtifactDeliverySecurityOptions>().BindConfiguration("ArtifactDelivery:Security");
        services.TryAddSingleton<IArtifactDeliverySignatureService, DefaultArtifactDeliverySignatureService>();
        services.TryAddSingleton<IArtifactDeliveryNonceStore, InMemoryArtifactDeliveryNonceStore>();
        services.TryAddScoped<IArtifactDeliverySecretResolver, ConfigurationArtifactDeliverySecretResolver>();
        services.TryAddScoped<IArtifactDeliveryHttpRequestSigner, DefaultArtifactDeliveryHttpRequestSigner>();
        services.TryAddScoped<IArtifactDeliveryEndpointAuthenticator, ArtifactDeliveryEndpointAuthenticator>();
        services.AddScoped<IArtifactPublicationApplicationService, ArtifactPublicationApplicationService>();
        services.AddScoped<IOrchestrationNodePolicyApplicationService, OrchestrationNodePolicyApplicationService>();
        services.AddScoped<IArtifactValidationPolicy, JsonArtifactValidationPolicy>();
        services.AddScoped<IArtifactBuilder<OrchestrationVersionDeployedEvent>, DeployedArtifactBuilder>();
        services.AddScoped<IArtifactBuilder<OrchestrationVersionDeprecatedEvent>, DeprecatedArtifactBuilder>();
        services.AddScoped<IArtifactBuilder<OrchestrationVersionArchivedEvent>, ArchivedArtifactBuilder>();
        return services;
    }
}


