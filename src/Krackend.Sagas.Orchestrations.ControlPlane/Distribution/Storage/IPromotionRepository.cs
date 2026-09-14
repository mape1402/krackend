using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

public interface IReleaseRepository
{
    Task Create(Release promotion, IReadOnlyCollection<ReleasePlanTarget> targets, CancellationToken cancellationToken = default);
    Task ApplyTargetStatus(Id releaseId, Id runtimeNodeId, ReleaseStatus status, CancellationToken cancellationToken = default);
    Task<Release> GetById(Id promotionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ReleasePlanTarget>> GetTargets(Id promotionId, CancellationToken cancellationToken = default);
    Task<PagedResult<Release>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default);
}


