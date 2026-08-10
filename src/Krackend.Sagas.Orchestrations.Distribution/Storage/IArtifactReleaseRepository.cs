using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Krackend.Sagas.Orchestrations.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage;

public interface IArtifactRepository
{
    Task Create(Artifact artifact, CancellationToken cancellationToken = default);
    Task SetPublished(Id artifactId, bool isPublished, CancellationToken cancellationToken = default);
    Task<Artifact> GetById(Id artifactId, CancellationToken cancellationToken = default);
    Task<PagedResult<Artifact>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default);
}

