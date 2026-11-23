using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;

/// <summary>
/// Indicates that an operation could not acquire the lease for an orchestration instance.
/// </summary>
public sealed class OrchestrationInstanceLeaseUnavailableException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationInstanceLeaseUnavailableException"/> class.
    /// </summary>
    /// <param name="instanceId">Orchestration instance id whose lease was unavailable.</param>
    public OrchestrationInstanceLeaseUnavailableException(Id instanceId)
        : base($"The lease for orchestration instance '{instanceId}' is unavailable. Another operation may hold it in this process or another runtime replica.")
    {
        InstanceId = instanceId;
    }

    /// <summary>
    /// Gets the orchestration instance id whose lease was unavailable.
    /// </summary>
    public Id InstanceId { get; }
}
