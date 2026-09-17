using Krackend.Sagas.Orchestrations.Runtime.Replication;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;

internal sealed class TestRuntimeReplicaIdentity : IRuntimeReplicaIdentity
{
    public string ReplicaId { get; set; } = "replica-a";

    public string ReplicaBootId { get; set; } = "replica-a-boot";

    public string LocalStandupLane { get; set; } = "runtime-standup:replica-a";
}
