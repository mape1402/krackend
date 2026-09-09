using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;

internal sealed class RecordingRuntimeArtifactRepository : IRuntimeArtifactRepository
{
    public Id? FailedArtifactId { get; private set; }

    public long? FailedIngressGeneration { get; private set; }

    public string FailedError { get; private set; } = string.Empty;

    public Task Upsert(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task MarkProjectionStarted(
        Id artifactId,
        long ingressGeneration,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task MarkReady(
        Id artifactId,
        long ingressGeneration,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task MarkProjectionFailed(
        Id artifactId,
        long ingressGeneration,
        string error,
        CancellationToken cancellationToken = default)
    {
        FailedArtifactId = artifactId;
        FailedIngressGeneration = ingressGeneration;
        FailedError = error;
        return Task.CompletedTask;
    }

    public Task DeactivateActiveArtifacts(
        string orchestrationDefinitionKey,
        Id exceptArtifactId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<RuntimeOrchestrationArtifact> GetById(
        Id artifactId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<RuntimeOrchestrationArtifact> GetByVersion(
        string orchestrationDefinitionKey,
        SemanticVersion version,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetAll(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetReady(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<RuntimeOrchestrationArtifact> GetActive(
        string orchestrationDefinitionKey,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
