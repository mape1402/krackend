using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;

namespace Krackend.Sagas.Orchestrations.Runtime.Api;

/// <summary>
/// Converts runtime domain models into API models.
/// </summary>
internal static class RuntimeApiModelMapper
{
    /// <summary>
    /// Converts one runtime artifact into a compact API row.
    /// </summary>
    public static RuntimeArtifactApiModel ToApiModel(RuntimeOrchestrationArtifact artifact)
        => new(
            artifact.Id.ToString(),
            artifact.OrchestrationDefinitionKey,
            artifact.ArtifactType,
            artifact.Version.ToString(),
            artifact.Status.ToString(),
            artifact.IsActive,
            artifact.IngressGeneration,
            artifact.ArtifactChecksum.Value,
            artifact.DeployedOnUtc,
            artifact.ActivatedOnUtc,
            artifact.ProjectionStartedOnUtc,
            artifact.ProjectionCompletedOnUtc,
            artifact.ProjectionFailedOnUtc,
            artifact.ProjectionError ?? string.Empty);

    /// <summary>
    /// Converts one runtime ingress configuration into an API row.
    /// </summary>
    public static RuntimeIngressConfigurationApiModel ToApiModel(RuntimeIngressConfiguration configuration)
        => new(
            configuration.Id.ToString(),
            configuration.RuntimeOrchestrationArtifactId.ToString(),
            configuration.ConfigurationKey,
            configuration.IngressKind.ToString(),
            configuration.IngressTransport.ToString(),
            configuration.SettingsPayload,
            configuration.IsActive,
            configuration.CreatedOnUtc,
            configuration.UpdatedOnUtc,
            configuration.DeactivatedOnUtc);

    /// <summary>
    /// Converts one runtime design node into a secret-safe API row.
    /// </summary>
    public static RuntimeDesignNodeApiModel ToApiModel(RuntimeDesignNode node)
        => new(
            node.Id.ToString(),
            node.Key,
            node.Name,
            node.EndpointBaseUri,
            node.RemoteRuntimeNodeId,
            node.DistributionMode.ToString(),
            node.AccessTokenTtlSeconds,
            node.TokenRefreshSkewSeconds,
            node.TokenValidationCacheTtlSeconds,
            node.InboundCredentialStatus.ToString(),
            node.OutboundCredentialStatus.ToString(),
            node.Description,
            node.Status.ToString(),
            node.IsEnabled,
            node.CreatedOnUtc,
            node.UpdatedOnUtc);
}
