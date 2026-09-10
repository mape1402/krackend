namespace Krackend.Sagas.Orchestrations.Runtime.Replication;

/// <summary>
/// Provides the local, ephemeral identity of the current runtime replica.
/// </summary>
public interface IRuntimeReplicaIdentity
{
    /// <summary>
    /// Gets the current runtime replica id.
    /// </summary>
    string ReplicaId { get; }

    /// <summary>
    /// Gets the ephemeral id assigned to this runtime replica process start.
    /// </summary>
    string ReplicaBootId { get; }

    /// <summary>
    /// Gets the Mule lane used for work that must execute in this replica.
    /// </summary>
    string LocalStandupLane { get; }
}
