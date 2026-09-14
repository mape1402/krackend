using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal sealed record StartStageDecision(
        Id InstanceId,
        StageArtifact Stage,
        string Payload) : IDecision
    {
        public string Kind => "start-stage";
    }
}
