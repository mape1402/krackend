using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts
{
    internal sealed class DefaultRuntimeArtifactResolver : IRuntimeArtifactResolver
    {
        private readonly IRuntimeArtifactRepository _repository;
        private readonly IRuntimeArtifactSerializer _serializer;

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

            var runtimeArtifact = await _repository.GetById(new Id(Ulid.Parse(artifactId)), cancellationToken);
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
