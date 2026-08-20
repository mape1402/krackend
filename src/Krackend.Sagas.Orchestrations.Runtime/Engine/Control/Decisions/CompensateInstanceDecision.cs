using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal sealed record CompensateInstanceDecision(
        Id InstanceId,
        string ArtifactId,
        string Payload) : IDecision
    {
        public string Kind => "compensate-instance";
    }
}
