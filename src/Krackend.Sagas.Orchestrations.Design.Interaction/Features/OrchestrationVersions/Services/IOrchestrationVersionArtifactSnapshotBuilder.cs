using Krackend.Sagas.Orchestrations.Design.Core;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

public interface IOrchestrationVersionArtifactSnapshotBuilder
{
    Task<OrchestrationVersion> Build(
        OrchestrationVersion version,
        CancellationToken cancellationToken = default);
}
