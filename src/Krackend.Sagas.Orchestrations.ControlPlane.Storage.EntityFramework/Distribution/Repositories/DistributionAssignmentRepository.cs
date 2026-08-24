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

public sealed class ReleaseTargetRepository : IReleaseTargetRepository
{
    private readonly ControlPlaneDbContext _dbContext;
    private readonly ISieveProcessor _sieveProcessor;

    public ReleaseTargetRepository(ControlPlaneDbContext dbContext, ISieveProcessor sieveProcessor)
    {
        _dbContext = dbContext;
        _sieveProcessor = sieveProcessor;
    }

    public async Task Create(ReleaseTarget assignment, CancellationToken cancellationToken = default)
    {
        _dbContext.ReleaseTargets.Add(new ReleaseTargetEntity
        {
            Id = assignment.Id,
            RuntimeNodeId = assignment.RuntimeNodeId,
            ArtifactId = assignment.ArtifactId,
            ReleaseId = assignment.ReleaseId,
            RolloutGroup = assignment.RolloutGroup,
            Status = assignment.Status,
            ActivationStatus = assignment.ActivationStatus,
            AssignedAtUtc = assignment.AssignedAtUtc,
            AvailableAtUtc = assignment.AvailableAtUtc,
            DeliveredAtUtc = assignment.DeliveredAtUtc,
            AcknowledgedAtUtc = assignment.AcknowledgedAtUtc,
            ActivatedAtUtc = assignment.ActivatedAtUtc,
            FailedAtUtc = assignment.FailedAtUtc,
            FailureReason = assignment.FailureReason,
            RuntimeVersionApplied = assignment.RuntimeVersionApplied,
            CorrelationId = assignment.CorrelationId
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task Update(ReleaseTarget assignment, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ReleaseTargets.FirstAsync(x => x.Id == assignment.Id, cancellationToken);
        entity.RuntimeNodeId = assignment.RuntimeNodeId;
        entity.ArtifactId = assignment.ArtifactId;
        entity.ReleaseId = assignment.ReleaseId;
        entity.RolloutGroup = assignment.RolloutGroup;
        entity.Status = assignment.Status;
        entity.ActivationStatus = assignment.ActivationStatus;
        entity.AssignedAtUtc = assignment.AssignedAtUtc;
        entity.AvailableAtUtc = assignment.AvailableAtUtc;
        entity.DeliveredAtUtc = assignment.DeliveredAtUtc;
        entity.AcknowledgedAtUtc = assignment.AcknowledgedAtUtc;
        entity.ActivatedAtUtc = assignment.ActivatedAtUtc;
        entity.FailedAtUtc = assignment.FailedAtUtc;
        entity.FailureReason = assignment.FailureReason;
        entity.RuntimeVersionApplied = assignment.RuntimeVersionApplied;
        entity.CorrelationId = assignment.CorrelationId;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAttempt(ReleaseAttempt attempt, CancellationToken cancellationToken = default)
    {
        _dbContext.ReleaseAttempts.Add(new ReleaseAttemptEntity
        {
            Id = attempt.Id,
            ReleaseTargetId = attempt.ReleaseTargetId,
            Action = attempt.Action,
            InitiatedBy = attempt.InitiatedBy,
            StartedAtUtc = attempt.StartedAtUtc,
            FinishedAtUtc = attempt.FinishedAtUtc,
            Succeeded = attempt.Succeeded,
            ErrorCode = attempt.ErrorCode,
            ErrorMessage = attempt.ErrorMessage,
            ExternalReference = attempt.ExternalReference
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReleaseTarget> GetById(Id assignmentId, CancellationToken cancellationToken = default)
    {
        var x = await _dbContext.ReleaseTargets.AsNoTracking().FirstAsync(y => y.Id == assignmentId, cancellationToken);
        return Map(x);
    }

    public async Task<ReleaseTarget> GetByArtifactAndRuntimeNode(
        Id artifactId,
        Id runtimeNodeId,
        CancellationToken cancellationToken = default)
    {
        var x = await _dbContext.ReleaseTargets.AsNoTracking()
            .Where(y => y.ArtifactId == artifactId && y.RuntimeNodeId == runtimeNodeId)
            .OrderByDescending(y => y.AssignedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return x is null ? null : Map(x);
    }

    public async Task<IReadOnlyCollection<ReleaseAttempt>> GetAttempts(Id assignmentId, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.ReleaseAttempts.AsNoTracking()
            .Where(x => x.ReleaseTargetId == assignmentId)
            .OrderByDescending(x => x.StartedAtUtc)
            .ToArrayAsync(cancellationToken);

        return rows.Select(x => new ReleaseAttempt
        {
            Id = x.Id,
            ReleaseTargetId = x.ReleaseTargetId,
            Action = x.Action,
            InitiatedBy = x.InitiatedBy,
            StartedAtUtc = x.StartedAtUtc,
            FinishedAtUtc = x.FinishedAtUtc,
            Succeeded = x.Succeeded,
            ErrorCode = x.ErrorCode,
            ErrorMessage = x.ErrorMessage,
            ExternalReference = x.ExternalReference
        }).ToArray();
    }

    public async Task<IReadOnlyCollection<ReleaseTarget>> GetPendingForRuntimeNode(Id runtimeNodeId, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.ReleaseTargets.AsNoTracking()
            .Where(x => x.RuntimeNodeId == runtimeNodeId
                && (x.Status == ReleaseTargetStatus.AvailableForPull
                    || x.Status == ReleaseTargetStatus.Pending))
            .OrderBy(x => x.AssignedAtUtc)
            .ToArrayAsync(cancellationToken);

        return rows.Select(Map).ToArray();
    }

    public async Task<PagedResult<ReleaseTarget>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ReleaseTargets.AsNoTracking().OrderByDescending(x => x.AssignedAtUtc);
        var processed = _sieveProcessor.Apply(new SieveModel { Page = pagedSettings.PageNumber, PageSize = pagedSettings.PageSize }, query);
        var rows = await processed.ToArrayAsync(cancellationToken);
        var totalRows = await query.CountAsync(cancellationToken);
        var totalPages = totalRows == 0 ? 1 : (int)Math.Ceiling(totalRows / (double)pagedSettings.PageSize);
        return new PagedResult<ReleaseTarget>(pagedSettings.PageNumber, totalPages, totalRows, pagedSettings.PageSize, rows.Select(Map).ToArray());
    }

    private static ReleaseTarget Map(ReleaseTargetEntity x) => new()
    {
        Id = x.Id,
        RuntimeNodeId = x.RuntimeNodeId,
        ArtifactId = x.ArtifactId,
        ReleaseId = x.ReleaseId,
        RolloutGroup = x.RolloutGroup,
        Status = x.Status,
        ActivationStatus = x.ActivationStatus,
        AssignedAtUtc = x.AssignedAtUtc,
        AvailableAtUtc = x.AvailableAtUtc,
        DeliveredAtUtc = x.DeliveredAtUtc,
        AcknowledgedAtUtc = x.AcknowledgedAtUtc,
        ActivatedAtUtc = x.ActivatedAtUtc,
        FailedAtUtc = x.FailedAtUtc,
        FailureReason = x.FailureReason,
        RuntimeVersionApplied = x.RuntimeVersionApplied,
        CorrelationId = x.CorrelationId
    };
}

