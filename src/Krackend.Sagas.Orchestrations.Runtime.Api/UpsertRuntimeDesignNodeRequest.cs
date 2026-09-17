using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

namespace Krackend.Sagas.Orchestrations.Runtime.Api;

/// <summary>
/// Represents a runtime design node upsert request.
/// </summary>
public sealed record UpsertRuntimeDesignNodeRequest(
    string DesignNodeId,
    string Key,
    string Name,
    string EndpointBaseUri,
    string RemoteRuntimeNodeId,
    DistributionConnectionMode DistributionMode,
    int AccessTokenTtlSeconds,
    int TokenRefreshSkewSeconds,
    int TokenValidationCacheTtlSeconds,
    string Description);
