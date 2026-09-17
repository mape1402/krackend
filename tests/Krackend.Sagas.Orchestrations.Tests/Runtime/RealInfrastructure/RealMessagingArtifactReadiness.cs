namespace Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

internal sealed record RealMessagingArtifactReadiness(
    RuntimeOrchestrationArtifact Artifact,
    bool AppliedLocally);
