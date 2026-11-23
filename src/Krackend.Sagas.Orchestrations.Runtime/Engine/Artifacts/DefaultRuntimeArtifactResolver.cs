using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts
{
    internal sealed class DefaultRuntimeArtifactResolver : IRuntimeArtifactResolver
    {
        private readonly IRuntimeArtifactRepository _repository;
        private readonly IRuntimeArtifactSerializer _serializer;
        private readonly RuntimeArtifactSnapshotCache _snapshotCache;
        private readonly Dictionary<Id, ResolvedOrchestrationArtifact> _activationCache = [];
        internal OrchestrationTimeoutBatchContext TimeoutBatch { get; set; }

        public DefaultRuntimeArtifactResolver(
            IRuntimeArtifactRepository repository,
            IRuntimeArtifactSerializer serializer,
            RuntimeArtifactSnapshotCache snapshotCache = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _snapshotCache = snapshotCache;
        }

        public async Task<ResolvedOrchestrationArtifact> ResolveAsync(string artifactId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(artifactId))
            {
                throw new ArgumentException("Artifact id cannot be empty.", nameof(artifactId));
            }

            var id = new Id(Ulid.Parse(artifactId));
            if (TimeoutBatch is not null)
                return await TimeoutBatch.ResolveAsync(id, this, cancellationToken);
            if (_activationCache.TryGetValue(id, out var cached))
                return cached;
            var resolved = _snapshotCache is null
                ? await ResolveUncachedAsync(id, cancellationToken)
                : await _snapshotCache.GetOrAddAsync(
                    id,
                    token => ResolveUncachedAsync(id, token),
                    cancellationToken);
            _activationCache[id] = resolved;
            return resolved;
        }

        internal async Task<ResolvedOrchestrationArtifact> ResolveUncachedAsync(Id id, CancellationToken cancellationToken)
        {
            var artifactId = id.ToString();
            var runtimeArtifact = await _repository.GetById(id, cancellationToken);
            if (!runtimeArtifact.IsActive)
            {
                throw new InvalidOperationException($"Runtime artifact '{artifactId}' is not active.");
            }

            var artifact = _serializer.Deserialize(runtimeArtifact.ArtifactPayload.ToJsonString());
            return new ResolvedOrchestrationArtifact
            {
                RuntimeArtifact = runtimeArtifact,
                Artifact = artifact
            };
        }
    }
}
