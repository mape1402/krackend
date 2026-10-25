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
    /// <summary>Searches artifact summaries in the backing store.</summary>
    Task<PagedResult<Artifact>> Search(PagedSettings settings, string orchestrationId, string search, string sort, bool descending, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured artifact storage does not support catalog search.");
    Task<IReadOnlyCollection<Artifact>> GetSummariesByIds(IReadOnlyCollection<Id> artifactIds, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured artifact storage does not support summary lookup.");
}

