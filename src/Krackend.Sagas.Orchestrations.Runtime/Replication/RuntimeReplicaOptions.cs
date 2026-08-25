namespace Krackend.Sagas.Orchestrations.Runtime.Replication;

/// <summary>
/// Configures the ephemeral identity used by one runtime replica.
/// </summary>
public sealed class RuntimeReplicaOptions
{
    /// <summary>
    /// Gets or sets the configured replica id. When empty, the runtime creates one from process environment data.
    /// </summary>
    public string ReplicaId { get; set; }

    /// <summary>
    /// Gets or sets the Mule lane prefix used for local standup actions.
    /// </summary>
    public string StandupLanePrefix { get; set; } = "runtime-standup";
}
