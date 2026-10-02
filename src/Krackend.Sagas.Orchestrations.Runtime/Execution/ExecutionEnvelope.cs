namespace Krackend.Sagas.Orchestrations.Runtime.Execution;

using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

/// <summary>
/// Carries the prepared runtime command and policy snapshot given to an execution provider.
/// </summary>
public sealed class ExecutionEnvelope
{
    /// <summary>
    /// Gets the task attempt request that produced the command.
    /// </summary>
    public required TaskRuntimeCommandRequest TaskRequest { get; init; }

    /// <summary>
    /// Gets the prepared command to dispatch.
    /// </summary>
    public required RemoteCommand Command { get; init; }

    /// <summary>
    /// Gets the resolved execution policy selected for this dispatch.
    /// </summary>
    public required ResolvedExecutionPolicyArtifact ResolvedPolicy { get; init; }
}
