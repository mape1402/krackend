using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts
{
    internal sealed class ResolvedOrchestrationArtifact
    {
        public required RuntimeOrchestrationArtifact RuntimeArtifact { get; init; }

        public required OrchestrationArtifact Artifact { get; init; }
    }
}
