using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
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
        services.AddHttpClient<IRuntimeNodeConnectionApplicationService, RuntimeNodeConnectionApplicationService>();
        services.AddScoped<IArtifactApplicationService, ArtifactApplicationService>();
        services.AddScoped<IReleaseApplicationService, ReleaseApplicationService>();
        services.AddScoped<IReleaseTargetApplicationService, ReleaseTargetApplicationService>();
        services.AddHttpClient<IArtifactDeliveryApplicationService, ArtifactDeliveryApplicationService>();
        services.TryAddSingleton<IDistributedCache, MemoryDistributedCache>();
        services.TryAddSingleton<IConnectionSecretHasher, Pbkdf2ConnectionSecretHasher>();
        services.TryAddSingleton<IConnectionSecretGenerator, SecureConnectionSecretGenerator>();
        services.TryAddSingleton<ITokenHashService, Sha256TokenHashService>();
        services.TryAddSingleton<IConnectionScopeFormatter, DefaultConnectionScopeFormatter>();
        services.TryAddSingleton<ConnectionTokenCacheKeyBuilder>();
        services.TryAddSingleton<ConnectionCredentialPackageSerializer>();
        services.AddDataProtection();
        services.TryAddSingleton<IControlPlaneRuntimeNodeSecretProtector, DataProtectionControlPlaneRuntimeNodeSecretProtector>();
        services.TryAddScoped<IControlPlaneConnectionTokenIssuer, ControlPlaneConnectionTokenIssuer>();
        services.TryAddScoped<IControlPlaneConnectionTokenValidator, ControlPlaneConnectionTokenValidator>();
        services.AddHttpClient<IRuntimeAccessTokenProvider, RuntimeAccessTokenProvider>();
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


