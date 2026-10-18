namespace Krackend.Sagas.Orchestrations.Runtime.Api;

/// <summary>
/// Represents a compact runtime artifact row returned by the REST API.
/// </summary>
public sealed record RuntimeArtifactApiModel(
    string Id,
    string OrchestrationDefinitionKey,
    string ArtifactType,
    string Version,
    string Status,
    string LifecycleStatus,
    bool IsActive,
    string LastReleaseId,
    string LastControlPlaneArtifactId,
    string ReleaseStrategy,
    long IngressGeneration,
    string Checksum,
    DateTime DeployedOnUtc,
    DateTime? ActivatedOnUtc,
    DateTime? ProjectionStartedOnUtc,
    DateTime? ProjectionCompletedOnUtc,
    DateTime? ProjectionFailedOnUtc,
    string ProjectionError);
