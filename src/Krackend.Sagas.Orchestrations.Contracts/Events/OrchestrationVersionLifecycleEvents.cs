namespace Krackend.Sagas.Orchestrations.Contracts.Events;

public sealed record OrchestrationVersionDeployedEvent(
    string OrchestrationVersionId,
    string OrchestrationDefinitionId,
    string OrchestrationDisplayName,
    string VersionLabel,
    string VersionNumber,
    string ArtifactPayloadJson,
    string Checksum,
    string Actor,
    string CorrelationId,
    DateTime OccurredAtUtc);

public sealed record OrchestrationVersionDeprecatedEvent(
    string OrchestrationVersionId,
    string OrchestrationDefinitionId,
    string OrchestrationDisplayName,
    string VersionLabel,
    string VersionNumber,
    string ArtifactPayloadJson,
    string Checksum,
    string Actor,
    string CorrelationId,
    DateTime OccurredAtUtc);

public sealed record OrchestrationVersionArchivedEvent(
    string OrchestrationVersionId,
    string OrchestrationDefinitionId,
    string OrchestrationDisplayName,
    string VersionLabel,
    string VersionNumber,
    string ArtifactPayloadJson,
    string Checksum,
    string Actor,
    string CorrelationId,
    DateTime OccurredAtUtc);
