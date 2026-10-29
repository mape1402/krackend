using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;

/// <summary>
/// Moves an orchestration into its bounded reconciliation window after normal execution and retry options are exhausted.
/// </summary>
internal sealed record EnterIdleInstanceDecision(
    Id InstanceId,
    Id? StageExecutionId,
    Id? TaskExecutionId,
    string Reason) : IDecision
{
    public string Kind => "enter-idle-instance";
}
