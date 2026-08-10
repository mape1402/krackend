using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Krackend.Sagas.Orchestrations.Distribution.Core;
using Krackend.Sagas.Orchestrations.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage;

public interface IReleaseRepository
{
    Task Create(Release promotion, IReadOnlyCollection<ReleasePlanTarget> targets, CancellationToken cancellationToken = default);
    Task ApplyTargetStatus(Id releaseId, Id runtimeNodeId, ReleaseStatus status, CancellationToken cancellationToken = default);
    Task<Release> GetById(Id promotionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ReleasePlanTarget>> GetTargets(Id promotionId, CancellationToken cancellationToken = default);
    Task<PagedResult<Release>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default);
}


