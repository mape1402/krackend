using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal sealed record DispatchTaskDecision(
        Id InstanceId,
        Id StageExecutionId,
        string StageKey,
        TaskArtifact Task,
        string Payload) : IDecision
    {
        public string Kind => "dispatch-task";
    }
}
