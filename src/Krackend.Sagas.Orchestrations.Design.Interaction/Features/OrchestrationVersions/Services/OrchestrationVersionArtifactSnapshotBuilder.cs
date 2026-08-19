using Krackend.Sagas.Orchestrations.Design.Core;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

public sealed class OrchestrationVersionArtifactSnapshotBuilder : IOrchestrationVersionArtifactSnapshotBuilder
{
    public Task<OrchestrationVersion> Build(
        OrchestrationVersion version,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        return Task.FromResult(version);
    }
}
