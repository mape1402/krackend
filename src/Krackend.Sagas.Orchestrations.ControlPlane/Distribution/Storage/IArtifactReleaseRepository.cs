using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

public interface IArtifactRepository
{
    Task Create(Artifact artifact, CancellationToken cancellationToken = default);
    Task SetPublished(Id artifactId, bool isPublished, CancellationToken cancellationToken = default);
    Task<Artifact> GetById(Id artifactId, CancellationToken cancellationToken = default);
    Task<Artifact> GetLatestForOrchestrationVersion(Id orchestrationVersionId, string artifactType, CancellationToken cancellationToken = default);
    Task<PagedResult<Artifact>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default);
}

