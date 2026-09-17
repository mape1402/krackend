namespace Krackend.Sagas.Orchestrations.Runtime.Api;

/// <summary>
/// Represents a runtime design node returned by the REST API.
/// </summary>
public sealed record RuntimeDesignNodeApiModel(
    string Id,
    string Key,
    string Name,
    string EndpointBaseUri,
    string RemoteRuntimeNodeId,
    string DistributionMode,
    int AccessTokenTtlSeconds,
    int TokenRefreshSkewSeconds,
    int TokenValidationCacheTtlSeconds,
    string InboundCredentialStatus,
    string OutboundCredentialStatus,
    string Description,
    string Status,
    bool IsEnabled,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc);
