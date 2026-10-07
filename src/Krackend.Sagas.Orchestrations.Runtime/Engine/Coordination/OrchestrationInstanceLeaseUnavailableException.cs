using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;

/// <summary>
/// Indicates that another runtime node currently owns the lease for an orchestration instance.
/// </summary>
public sealed class OrchestrationInstanceLeaseUnavailableException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationInstanceLeaseUnavailableException"/> class.
    /// </summary>
    /// <param name="instanceId">Orchestration instance id whose lease was unavailable.</param>
    public OrchestrationInstanceLeaseUnavailableException(Id instanceId)
        : base($"Orchestration instance '{instanceId}' is already being processed by another runtime node.")
    {
        InstanceId = instanceId;
    }

    /// <summary>
    /// Gets the orchestration instance id whose lease was unavailable.
    /// </summary>
    public Id InstanceId { get; }
}
