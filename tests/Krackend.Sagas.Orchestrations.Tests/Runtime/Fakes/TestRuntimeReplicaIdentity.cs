using Krackend.Sagas.Orchestrations.Runtime.Replication;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;

internal sealed class TestRuntimeReplicaIdentity : IRuntimeReplicaIdentity
{
    public string ReplicaId { get; init; } = "replica-a";

    public string ReplicaBootId { get; init; } = "replica-a-boot";

    public string LocalStandupLane { get; init; } = "runtime-standup:replica-a";
}
