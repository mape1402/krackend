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
    Task<IReadOnlyCollection<ReleasePlanTarget>> GetTargetsByReleases(IReadOnlyCollection<Id> releaseIds, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured release storage does not support batch target lookup.");
    Task<PagedResult<Release>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default);
    Task<PagedResult<Release>> Search(PagedSettings settings, string orchestrationId, string search, string sort, bool descending, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured release storage does not support catalog search.");
    Task<IReadOnlyCollection<ReleaseCatalogSummary>> GetSummaries(IReadOnlyCollection<string> orchestrationIds, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured release storage does not support summary lookup.");
    Task<PagedResult<ReleaseTargetDetail>> SearchTargetDetails(Id releaseId, PagedSettings settings, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured release storage does not support paged release details.");
    Task<IReadOnlyCollection<ReleaseEffectiveStatus>> GetEffectiveStatuses(IReadOnlyCollection<Id> releaseIds, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured release storage does not support status summaries.");
}

public sealed record ReleaseCatalogSummary(string OrchestrationId, int Count, DateTime LatestCreatedAtUtc);
public sealed record ReleaseTargetDetail(string RuntimeNodeId, string Name, string Code, string Environment,
    string Mode, string Status, string TargetId);
public sealed record ReleaseEffectiveStatus(Id ReleaseId, bool AllActivated, bool AnyFailed);


