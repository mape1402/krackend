namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Support;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

internal sealed class TestRuntimeArtifactDeploymentRepository : IRuntimeArtifactRepository
{
    private readonly Dictionary<Id, RuntimeOrchestrationArtifact> _artifacts = [];

    public int UpsertCount { get; private set; }

    public IReadOnlyCollection<RuntimeOrchestrationArtifact> Artifacts => _artifacts.Values.ToArray();

    public RuntimeOrchestrationArtifact Add(RuntimeOrchestrationArtifact artifact)
    {
        _artifacts[artifact.Id] = artifact;
        return artifact;
    }

    public Task Upsert(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default)
    {
        UpsertCount++;
        _artifacts[artifact.Id] = artifact;
        return Task.CompletedTask;
    }

    public Task MarkProjectionStarted(
        Id artifactId,
        long ingressGeneration,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task MarkReady(
        Id artifactId,
        long ingressGeneration,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task MarkProjectionFailed(
        Id artifactId,
        long ingressGeneration,
        string error,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeactivateActiveArtifacts(
        string orchestrationDefinitionKey,
        Id exceptArtifactId,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<RuntimeOrchestrationArtifact> GetById(
        Id artifactId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_artifacts.TryGetValue(artifactId, out var artifact)
            ? artifact
            : throw new KeyNotFoundException());

    public Task<RuntimeOrchestrationArtifact> GetByVersion(
        string orchestrationDefinitionKey,
        SemanticVersion version,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_artifacts.Values.FirstOrDefault(artifact =>
                artifact.OrchestrationDefinitionKey == orchestrationDefinitionKey &&
                artifact.Version.CompareTo(version) == 0)
            ?? throw new KeyNotFoundException());

    public Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetAll(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<RuntimeOrchestrationArtifact>>(_artifacts.Values.ToArray());

    public Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetReady(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<RuntimeOrchestrationArtifact>>(
            _artifacts.Values.Where(artifact => artifact.Status == RuntimeOrchestrationArtifactStatus.Ready).ToArray());

    public Task<RuntimeOrchestrationArtifact> GetActive(
        string orchestrationDefinitionKey,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_artifacts.Values.FirstOrDefault(artifact =>
                artifact.OrchestrationDefinitionKey == orchestrationDefinitionKey &&
                artifact.IsActive &&
                artifact.Status == RuntimeOrchestrationArtifactStatus.Ready)
            ?? throw new KeyNotFoundException());
}
