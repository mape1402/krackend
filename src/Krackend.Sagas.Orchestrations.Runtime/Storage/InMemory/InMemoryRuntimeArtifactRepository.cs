using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory
{
    internal sealed class InMemoryRuntimeArtifactRepository : IRuntimeArtifactRepository
    {
        private readonly InMemoryRuntimeStore _store;
        private readonly IRuntimeIngressConfigurationRepository _ingressConfigurationRepository;

        public InMemoryRuntimeArtifactRepository(
            InMemoryRuntimeStore store,
            IRuntimeIngressConfigurationRepository ingressConfigurationRepository)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _ingressConfigurationRepository = ingressConfigurationRepository ?? throw new ArgumentNullException(nameof(ingressConfigurationRepository));
        }

        public Task Upsert(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default)
        {
            _store.Artifacts[artifact.Id] = artifact;
            return Task.CompletedTask;
        }

        public Task MarkProjectionStarted(
            Id artifactId,
            long ingressGeneration,
            CancellationToken cancellationToken = default)
        {
            if (!_store.Artifacts.TryGetValue(artifactId, out var artifact) || artifact.IngressGeneration != ingressGeneration)
            {
                return Task.CompletedTask;
            }

            artifact.Status = RuntimeOrchestrationArtifactStatus.Pending;
            artifact.ProjectionStartedOnUtc = DateTime.UtcNow;
            artifact.ProjectionCompletedOnUtc = null;
            artifact.ProjectionFailedOnUtc = null;
            artifact.ProjectionError = null;
            return Task.CompletedTask;
        }

        public Task MarkReady(
            Id artifactId,
            long ingressGeneration,
            CancellationToken cancellationToken = default)
        {
            if (!_store.Artifacts.TryGetValue(artifactId, out var artifact) || artifact.IngressGeneration != ingressGeneration)
            {
                return Task.CompletedTask;
            }

            var now = DateTime.UtcNow;
            artifact.Status = RuntimeOrchestrationArtifactStatus.Ready;
            artifact.ActivatedOnUtc = now;
            artifact.ProjectionCompletedOnUtc = now;
            artifact.ProjectionFailedOnUtc = null;
            artifact.ProjectionError = null;
            return Task.CompletedTask;
        }

        public Task MarkProjectionFailed(
            Id artifactId,
            long ingressGeneration,
            string error,
            CancellationToken cancellationToken = default)
        {
            if (!_store.Artifacts.TryGetValue(artifactId, out var artifact) || artifact.IngressGeneration != ingressGeneration)
            {
                return Task.CompletedTask;
            }

            artifact.Status = RuntimeOrchestrationArtifactStatus.Failed;
            artifact.ProjectionFailedOnUtc = DateTime.UtcNow;
            artifact.ProjectionError = error;
            return Task.CompletedTask;
        }

        public async Task DeactivateActiveArtifacts(string orchestrationDefinitionKey, Id exceptArtifactId, CancellationToken cancellationToken = default)
        {
            var deactivatedArtifactIds = new List<Id>();
            foreach (var artifact in _store.Artifacts.Values.Where(x =>
                x.OrchestrationDefinitionKey == orchestrationDefinitionKey &&
                x.Id != exceptArtifactId &&
                x.IsActive))
            {
                artifact.IsActive = false;
                artifact.Status = RuntimeOrchestrationArtifactStatus.Retired;
                artifact.RetiredOnUtc = DateTime.UtcNow;
                deactivatedArtifactIds.Add(artifact.Id);
            }

            await _ingressConfigurationRepository.DeactivateForArtifactsAsync(deactivatedArtifactIds, cancellationToken);
        }

        public Task<RuntimeOrchestrationArtifact> GetById(Id artifactId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Artifacts.TryGetValue(artifactId, out var artifact)
                ? artifact
                : throw new KeyNotFoundException($"Runtime artifact '{artifactId}' was not found."));

        public Task<RuntimeOrchestrationArtifact> GetByVersion(string orchestrationDefinitionKey, SemanticVersion version, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Artifacts.Values.FirstOrDefault(x =>
                    x.OrchestrationDefinitionKey == orchestrationDefinitionKey &&
                    x.Version.Equals(version))
                ?? throw new KeyNotFoundException($"Runtime artifact '{orchestrationDefinitionKey}' version '{version}' was not found."));

        public Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetAll(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<RuntimeOrchestrationArtifact>>(
                _store.Artifacts.Values.ToArray());

        public Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetReady(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<RuntimeOrchestrationArtifact>>(
                _store.Artifacts.Values
                    .Where(x => x.IsActive &&
                        x.Status == RuntimeOrchestrationArtifactStatus.Ready)
                    .ToArray());

        public Task<RuntimeOrchestrationArtifact> GetActive(string orchestrationDefinitionKey, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Artifacts.Values.FirstOrDefault(x =>
                    x.OrchestrationDefinitionKey == orchestrationDefinitionKey &&
                    x.IsActive &&
                    x.Status == RuntimeOrchestrationArtifactStatus.Ready)
                ?? throw new KeyNotFoundException($"Active runtime artifact '{orchestrationDefinitionKey}' was not found."));
    }
}
