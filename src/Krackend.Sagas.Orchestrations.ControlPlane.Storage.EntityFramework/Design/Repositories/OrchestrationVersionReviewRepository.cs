namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Repositories;

using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;

/// <summary>
/// Entity Framework storage for independent orchestration version reviews.
/// </summary>
public sealed class OrchestrationVersionReviewRepository : IOrchestrationVersionReviewRepository
{
    private readonly ControlPlaneDbContext _dbContext;

    /// <summary>Initializes a new instance.</summary>
    public OrchestrationVersionReviewRepository(ControlPlaneDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task Create(OrchestrationVersionReview review, CancellationToken cancellationToken = default)
    {
        review.StateToken = EnsureStateToken(review.StateToken);
        _dbContext.Set<OrchestrationVersionReviewEntity>().Add(ToEntity(review));
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new InvalidOperationException("This orchestration version already has a review awaiting application.", exception);
        }
    }

    /// <inheritdoc />
    public async Task<OrchestrationVersionReview> GetById(Id reviewId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Set<OrchestrationVersionReviewEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == reviewId, cancellationToken)
            ?? throw new KeyNotFoundException($"OrchestrationVersionReview '{reviewId}' was not found.");

        return ToDomain(entity);
    }

    /// <inheritdoc />
    public async Task<OrchestrationVersionReview> GetPendingForVersion(Id orchestrationVersionId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Set<OrchestrationVersionReviewEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.OrchestrationVersionId == orchestrationVersionId
                    && x.ApplicationStatus == OrchestrationVersionReviewApplicationStatus.Pending,
                cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<OrchestrationVersionReview>> GetAllForVersion(
        Id orchestrationVersionId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Set<OrchestrationVersionReviewEntity>()
            .AsNoTracking()
            .Where(x => x.OrchestrationVersionId == orchestrationVersionId)
            .OrderByDescending(x => x.RequestedOnUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToArray();
    }

    /// <inheritdoc />
    public async Task Update(OrchestrationVersionReview review, CancellationToken cancellationToken = default)
    {
        var entitySet = _dbContext.Set<OrchestrationVersionReviewEntity>();
        var local = entitySet.Local.FirstOrDefault(x => x.Id == review.Id);
        if (local is not null)
        {
            _dbContext.Entry(local).State = EntityState.Detached;
        }

        // Existing databases can contain reviews created before the token was introduced.
        // Their empty token remains a valid original value for this first guarded update.
        var expectedStateToken = review.StateToken ?? string.Empty;
        var nextStateToken = CreateStateToken();
        var next = ToEntity(review);
        next.StateToken = nextStateToken;
        _dbContext.Attach(next);
        var entry = _dbContext.Entry(next);
        entry.State = EntityState.Modified;
        entry.Property(x => x.StateToken).OriginalValue = expectedStateToken;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            review.StateToken = nextStateToken;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            entry.State = EntityState.Detached;
            throw new InvalidOperationException(
                "The lifecycle review was updated by another actor. Reload it and try again.",
                exception);
        }
    }

    private static OrchestrationVersionReviewEntity ToEntity(OrchestrationVersionReview review)
        => new()
        {
            Id = review.Id,
            OrchestrationVersionId = review.OrchestrationVersionId,
            Operation = review.Operation,
            FromStatus = review.FromStatus,
            PendingStatus = review.PendingStatus,
            TargetStatus = review.TargetStatus,
            ExpectedChecksum = review.ExpectedChecksum,
            ApprovalStatus = review.ApprovalStatus,
            ApplicationStatus = review.ApplicationStatus,
            RequestedBy = review.RequestedBy,
            RequestedOnUtc = review.RequestedOnUtc,
            DecidedBy = review.DecidedBy,
            DecidedOnUtc = review.DecidedOnUtc,
            RejectionComment = review.RejectionComment,
            AbortedBy = review.AbortedBy,
            AbortedOnUtc = review.AbortedOnUtc,
            AbortReason = review.AbortReason,
            AppliedBy = review.AppliedBy,
            AppliedOnUtc = review.AppliedOnUtc,
            DeployedArtifactId = review.DeployedArtifactId,
            PendingSlot = GetPendingSlot(review),
            StateToken = review.StateToken ?? string.Empty,
        };

    private static OrchestrationVersionReview ToDomain(OrchestrationVersionReviewEntity entity)
        => new()
        {
            Id = entity.Id,
            OrchestrationVersionId = entity.OrchestrationVersionId,
            Operation = entity.Operation,
            FromStatus = entity.FromStatus,
            PendingStatus = entity.PendingStatus,
            TargetStatus = entity.TargetStatus,
            ExpectedChecksum = entity.ExpectedChecksum,
            ApprovalStatus = entity.ApprovalStatus,
            ApplicationStatus = entity.ApplicationStatus,
            RequestedBy = entity.RequestedBy,
            RequestedOnUtc = entity.RequestedOnUtc,
            DecidedBy = entity.DecidedBy,
            DecidedOnUtc = entity.DecidedOnUtc,
            RejectionComment = entity.RejectionComment,
            AbortedBy = entity.AbortedBy,
            AbortedOnUtc = entity.AbortedOnUtc,
            AbortReason = entity.AbortReason,
            AppliedBy = entity.AppliedBy,
            AppliedOnUtc = entity.AppliedOnUtc,
            DeployedArtifactId = entity.DeployedArtifactId,
            StateToken = entity.StateToken ?? string.Empty,
        };

    private static string GetPendingSlot(OrchestrationVersionReview review)
        => review.ApplicationStatus == OrchestrationVersionReviewApplicationStatus.Pending
            ? $"pending:{review.OrchestrationVersionId}"
            : $"closed:{review.Id}";

    private static string EnsureStateToken(string stateToken)
        => string.IsNullOrWhiteSpace(stateToken) ? CreateStateToken() : stateToken;

    private static string CreateStateToken()
        => Guid.NewGuid().ToString("N");
}
