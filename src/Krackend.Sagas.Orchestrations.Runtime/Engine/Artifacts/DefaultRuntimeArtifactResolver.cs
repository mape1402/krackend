using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts
{
    internal sealed class DefaultRuntimeArtifactResolver : IRuntimeArtifactResolver
    {
        private readonly IRuntimeArtifactRepository _repository;
        private readonly IRuntimeArtifactSerializer _serializer;
        internal OrchestrationTimeoutBatchContext TimeoutBatch { get; set; }

        public DefaultRuntimeArtifactResolver(
            IRuntimeArtifactRepository repository,
            IRuntimeArtifactSerializer serializer)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
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
            return await ResolveUncachedAsync(id, cancellationToken);
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
