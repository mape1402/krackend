using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Sieve.Models;
using Sieve.Services;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Repositories;

public sealed class ReleaseRepository : IReleaseRepository
{
    private readonly ControlPlaneDbContext _dbContext;
    private readonly ISieveProcessor _sieveProcessor;

    public ReleaseRepository(ControlPlaneDbContext dbContext, ISieveProcessor sieveProcessor)
    {
        _dbContext = dbContext;
        _sieveProcessor = sieveProcessor;
    }

    public async Task Create(Release release, IReadOnlyCollection<ReleasePlanTarget> targets, CancellationToken cancellationToken = default)
    {
        _dbContext.Releases.Add(new ReleaseEntity
        {
            Id = release.Id,
            ArtifactId = release.ArtifactId,
            OrchestrationDefinitionId = release.OrchestrationDefinitionId,
            RequestedBy = release.RequestedBy,
            Strategy = release.Strategy,
            Status = release.Status,
            CreatedAtUtc = release.CreatedAtUtc,
            CompletedAtUtc = release.CompletedAtUtc
        });

        _dbContext.ReleasePlanTargets.AddRange(targets.Select(t => new ReleasePlanTargetEntity
        {
            Id = t.Id,
            ReleaseId = t.ReleaseId,
            RuntimeNodeId = t.RuntimeNodeId,
            Status = t.Status,
            CreatedAtUtc = t.CreatedAtUtc,
            CompletedAtUtc = t.CompletedAtUtc,
            Notes = t.Notes
        }));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyTargetStatus(
        Id releaseId,
        Id runtimeNodeId,
        ReleaseStatus status,
        CancellationToken cancellationToken = default)
    {
        var target = await _dbContext.ReleasePlanTargets.FirstAsync(
            x => x.ReleaseId == releaseId && x.RuntimeNodeId == runtimeNodeId,
            cancellationToken);
        target.Status = status;
        target.CompletedAtUtc = status is ReleaseStatus.Completed or ReleaseStatus.Failed or ReleaseStatus.Cancelled
            ? DateTime.UtcNow
            : null;

        var release = await _dbContext.Releases.FirstAsync(x => x.Id == releaseId, cancellationToken);
        var targetStatuses = await _dbContext.ReleasePlanTargets
            .Where(x => x.ReleaseId == releaseId)
            .Select(x => new { x.RuntimeNodeId, x.Status })
            .ToArrayAsync(cancellationToken);
        var effectiveStatuses = targetStatuses
            .Select(x => x.RuntimeNodeId == runtimeNodeId ? status : x.Status)
            .ToArray();

        if (effectiveStatuses.Length > 0 && effectiveStatuses.All(x => x == ReleaseStatus.Completed))
        {
            release.Status = ReleaseStatus.Completed;
            release.CompletedAtUtc = DateTime.UtcNow;
        }
        else if (effectiveStatuses.Any(x => x == ReleaseStatus.Failed))
        {
            release.Status = ReleaseStatus.Failed;
            release.CompletedAtUtc = DateTime.UtcNow;
        }
        else
        {
            release.Status = ReleaseStatus.InProgress;
            release.CompletedAtUtc = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Release> GetById(Id releaseId, CancellationToken cancellationToken = default)
    {
        var x = await _dbContext.Releases.AsNoTracking().FirstOrDefaultAsync(y => y.Id == releaseId, cancellationToken);
        if (x is null) return null;
        return new Release
        {
            Id = x.Id,
            ArtifactId = x.ArtifactId,
            OrchestrationDefinitionId = x.OrchestrationDefinitionId,
            RequestedBy = x.RequestedBy,
            Strategy = x.Strategy,
            Status = x.Status,
            CreatedAtUtc = x.CreatedAtUtc,
            CompletedAtUtc = x.CompletedAtUtc
        };
    }

    public async Task<IReadOnlyCollection<ReleasePlanTarget>> GetTargets(Id releaseId, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.ReleasePlanTargets.AsNoTracking().Where(x => x.ReleaseId == releaseId).ToArrayAsync(cancellationToken);
        return rows.Select(x => new ReleasePlanTarget
        {
            Id = x.Id,
            ReleaseId = x.ReleaseId,
            RuntimeNodeId = x.RuntimeNodeId,
            Status = x.Status,
            CreatedAtUtc = x.CreatedAtUtc,
            CompletedAtUtc = x.CompletedAtUtc,
            Notes = x.Notes
        }).ToArray();
    }

    public async Task<PagedResult<Release>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Releases.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc);
        var processed = _sieveProcessor.Apply(new SieveModel { Page = pagedSettings.PageNumber, PageSize = pagedSettings.PageSize }, query);
        var rows = await processed.ToArrayAsync(cancellationToken);
        var totalRows = await query.CountAsync(cancellationToken);
        var totalPages = totalRows == 0 ? 1 : (int)Math.Ceiling(totalRows / (double)pagedSettings.PageSize);
        return new PagedResult<Release>(
            pagedSettings.PageNumber,
            totalPages,
            totalRows,
            pagedSettings.PageSize,
            rows.Select(x => new Release
            {
                Id = x.Id,
                ArtifactId = x.ArtifactId,
                OrchestrationDefinitionId = x.OrchestrationDefinitionId,
                RequestedBy = x.RequestedBy,
                Strategy = x.Strategy,
                Status = x.Status,
                CreatedAtUtc = x.CreatedAtUtc,
                CompletedAtUtc = x.CompletedAtUtc
            }).ToArray());
    }

    public async Task<IReadOnlyCollection<ReleasePlanTarget>> GetTargetsByReleases(IReadOnlyCollection<Id> releaseIds, CancellationToken cancellationToken = default)
    {
        if (releaseIds.Count == 0) return Array.Empty<ReleasePlanTarget>();
        var ids = releaseIds.ToArray();
        var rows = await _dbContext.ReleasePlanTargets.AsNoTracking().Where(x => ids.Contains(x.ReleaseId)).ToArrayAsync(cancellationToken);
        return rows.Select(x => new ReleasePlanTarget
        {
            Id = x.Id, ReleaseId = x.ReleaseId, RuntimeNodeId = x.RuntimeNodeId, Status = x.Status,
            CreatedAtUtc = x.CreatedAtUtc, CompletedAtUtc = x.CompletedAtUtc, Notes = x.Notes
        }).ToArray();
    }

    public async Task<IReadOnlyCollection<ReleaseCatalogSummary>> GetSummaries(IReadOnlyCollection<string> orchestrationIds, CancellationToken cancellationToken = default)
    {
        if (orchestrationIds.Count == 0) return Array.Empty<ReleaseCatalogSummary>();
        var ids = orchestrationIds.ToArray();
        return await _dbContext.Releases.AsNoTracking().Where(x => ids.Contains(x.OrchestrationDefinitionId))
            .GroupBy(x => x.OrchestrationDefinitionId)
            .Select(group => new ReleaseCatalogSummary(group.Key, group.Count(), group.Max(x => x.CreatedAtUtc)))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<PagedResult<ReleaseTargetDetail>> SearchTargetDetails(Id releaseId, PagedSettings settings, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, settings?.PageNumber ?? 1);
        var size = Math.Clamp(settings?.PageSize ?? 25, 1, 100);
        var query = _dbContext.ReleasePlanTargets.AsNoTracking().Where(x => x.ReleaseId == releaseId);
        var total = await query.LongCountAsync(cancellationToken);
        var rows = await query.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size)
            .Select(x => new
            {
                x.RuntimeNodeId, x.Status,
                Name = x.RuntimeNode.Name, Code = x.RuntimeNode.Code,
                Environment = x.RuntimeNode.Environment.Name, Mode = x.RuntimeNode.DistributionMode,
                Assignment = _dbContext.ReleaseTargets.Where(a => a.ReleaseId == releaseId && a.RuntimeNodeId == x.RuntimeNodeId)
                    .OrderByDescending(a => a.AssignedAtUtc).ThenByDescending(a => a.Id)
                    .Select(a => new { a.Id, a.Status }).FirstOrDefault()
            }).ToArrayAsync(cancellationToken);
        return new PagedResult<ReleaseTargetDetail>(page, total == 0 ? 1 : (int)Math.Ceiling(total / (double)size), total, size,
            rows.Select(x => new ReleaseTargetDetail(x.RuntimeNodeId.ToString(), x.Name ?? x.RuntimeNodeId.ToString(),
                x.Code ?? "Unknown code", x.Environment ?? "Unknown environment", x.Mode.ToString(),
                x.Assignment?.Status.ToString() ?? x.Status.ToString(), x.Assignment?.Id.ToString() ?? string.Empty)).ToArray());
    }

    public async Task<IReadOnlyCollection<ReleaseEffectiveStatus>> GetEffectiveStatuses(IReadOnlyCollection<Id> releaseIds, CancellationToken cancellationToken = default)
    {
        if (releaseIds.Count == 0) return Array.Empty<ReleaseEffectiveStatus>();
        var ids = releaseIds.ToArray();
        var rows = await _dbContext.Releases.AsNoTracking()
            .Where(r => ids.Contains(r.Id) && _dbContext.ReleasePlanTargets.Any(p => p.ReleaseId == r.Id))
            .Select(r => new
        {
            r.Id,
            AllActivated = !_dbContext.ReleasePlanTargets.Where(p => p.ReleaseId == r.Id).Any(p =>
                _dbContext.ReleaseTargets.Where(a => a.ReleaseId == r.Id && a.RuntimeNodeId == p.RuntimeNodeId)
                    .OrderByDescending(a => a.AssignedAtUtc).ThenByDescending(a => a.Id)
                    .Select(a => (ReleaseTargetStatus?)a.Status).FirstOrDefault() != ReleaseTargetStatus.Activated),
            AnyFailed = _dbContext.ReleasePlanTargets.Where(p => p.ReleaseId == r.Id).Any(p =>
                _dbContext.ReleaseTargets.Where(a => a.ReleaseId == r.Id && a.RuntimeNodeId == p.RuntimeNodeId)
                    .OrderByDescending(a => a.AssignedAtUtc).ThenByDescending(a => a.Id)
                    .Select(a => (ReleaseTargetStatus?)a.Status).FirstOrDefault() == ReleaseTargetStatus.Failed ||
                (!_dbContext.ReleaseTargets.Any(a => a.ReleaseId == r.Id && a.RuntimeNodeId == p.RuntimeNodeId) && p.Status == ReleaseStatus.Failed))
        }).ToArrayAsync(cancellationToken);
        return rows.Select(x => new ReleaseEffectiveStatus(x.Id, x.AllActivated, x.AnyFailed)).ToArray();
    }

    public async Task<PagedResult<Release>> Search(PagedSettings settings, string orchestrationId, string search, string sort, bool descending, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, settings?.PageNumber ?? 1);
        var size = Math.Clamp(settings?.PageSize ?? 25, 1, 100);
        var query = _dbContext.Releases.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(orchestrationId))
            query = query.Where(x => x.OrchestrationDefinitionId == orchestrationId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.RequestedBy.Contains(term) || x.Strategy.Contains(term) ||
                x.OrchestrationDefinitionId.Contains(term));
        }

        var total = await query.LongCountAsync(cancellationToken);
        var ordered = (sort, descending) switch
        {
            ("requestedBy", false) => query.OrderBy(x => x.RequestedBy),
            ("requestedBy", true) => query.OrderByDescending(x => x.RequestedBy),
            ("strategy", false) => query.OrderBy(x => x.Strategy),
            ("strategy", true) => query.OrderByDescending(x => x.Strategy),
            ("status", false) => query.OrderBy(x => x.Status),
            ("status", true) => query.OrderByDescending(x => x.Status),
            ("created", false) => query.OrderBy(x => x.CreatedAtUtc),
            _ => query.OrderByDescending(x => x.CreatedAtUtc)
        };
        var rows = await ordered.ThenBy(x => x.Id).Skip((page - 1) * size).Take(size).ToArrayAsync(cancellationToken);
        return new PagedResult<Release>(page, total == 0 ? 1 : (int)Math.Ceiling(total / (double)size), total, size,
            rows.Select(x => new Release
            {
                Id = x.Id, ArtifactId = x.ArtifactId, OrchestrationDefinitionId = x.OrchestrationDefinitionId,
                RequestedBy = x.RequestedBy, Strategy = x.Strategy, Status = x.Status,
                CreatedAtUtc = x.CreatedAtUtc, CompletedAtUtc = x.CompletedAtUtc
            }).ToArray());
    }
}


