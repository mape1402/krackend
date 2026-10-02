namespace Krackend.Sagas.Orchestrations.Runtime.Execution;

using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

internal sealed class ExecutionEnvelope
{
    public required TaskRuntimeCommandRequest TaskRequest { get; init; }

    public required RemoteCommand Command { get; init; }

    public required ResolvedExecutionPolicyArtifact ResolvedPolicy { get; init; }
}

