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
        var x = await _dbContext.Releases.AsNoTracking().FirstAsync(y => y.Id == releaseId, cancellationToken);
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
}


