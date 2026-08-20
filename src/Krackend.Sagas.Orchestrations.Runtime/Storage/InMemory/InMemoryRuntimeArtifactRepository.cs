using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory
{
    internal sealed class InMemoryRuntimeArtifactRepository : IRuntimeArtifactRepository
    {
        private readonly InMemoryRuntimeStore _store;
        private readonly IRuntimeIngressConfigurationProjector _ingressConfigurationProjector;
        private readonly IRuntimeIngressConfigurationRepository _ingressConfigurationRepository;

        public InMemoryRuntimeArtifactRepository(
            InMemoryRuntimeStore store,
            IRuntimeIngressConfigurationProjector ingressConfigurationProjector,
            IRuntimeIngressConfigurationRepository ingressConfigurationRepository)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _ingressConfigurationProjector = ingressConfigurationProjector ?? throw new ArgumentNullException(nameof(ingressConfigurationProjector));
            _ingressConfigurationRepository = ingressConfigurationRepository ?? throw new ArgumentNullException(nameof(ingressConfigurationRepository));
        }

        public async Task Upsert(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default)
        {
            _store.Artifacts[artifact.Id] = artifact;
            await _ingressConfigurationProjector.ProjectAsync(artifact, cancellationToken);
        }

        public async Task DeactivateActiveArtifacts(string environmentKey, string orchestrationDefinitionKey, Id exceptArtifactId, CancellationToken cancellationToken = default)
        {
            var deactivatedArtifactIds = new List<Id>();
            foreach (var artifact in _store.Artifacts.Values.Where(x =>
                x.EnvironmentKey == environmentKey &&
                x.OrchestrationDefinitionKey == orchestrationDefinitionKey &&
                x.Id != exceptArtifactId &&
                x.IsActive))
            {
                artifact.IsActive = false;
                artifact.RetiredOnUtc = DateTime.UtcNow;
                deactivatedArtifactIds.Add(artifact.Id);
            }

            await _ingressConfigurationRepository.DeactivateForArtifactsAsync(deactivatedArtifactIds, cancellationToken);
        }

        public Task<RuntimeOrchestrationArtifact> GetById(Id artifactId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Artifacts.TryGetValue(artifactId, out var artifact)
                ? artifact
                : throw new KeyNotFoundException($"Runtime artifact '{artifactId}' was not found."));

        public Task<RuntimeOrchestrationArtifact> GetByVersion(string environmentKey, string orchestrationDefinitionKey, SemanticVersion version, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Artifacts.Values.FirstOrDefault(x =>
                    x.EnvironmentKey == environmentKey &&
                    x.OrchestrationDefinitionKey == orchestrationDefinitionKey &&
                    x.Version.Equals(version))
                ?? throw new KeyNotFoundException($"Runtime artifact '{orchestrationDefinitionKey}' version '{version}' was not found."));

        public Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetAll(string environmentKey, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<RuntimeOrchestrationArtifact>>(
                _store.Artifacts.Values.Where(x => x.EnvironmentKey == environmentKey).ToArray());

        public Task<RuntimeOrchestrationArtifact> GetActive(string environmentKey, string orchestrationDefinitionKey, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Artifacts.Values.FirstOrDefault(x =>
                    x.EnvironmentKey == environmentKey &&
                    x.OrchestrationDefinitionKey == orchestrationDefinitionKey &&
                    x.IsActive)
                ?? throw new KeyNotFoundException($"Active runtime artifact '{orchestrationDefinitionKey}' was not found."));
    }
}
