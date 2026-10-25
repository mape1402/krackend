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
    public async Task<PagedResult<OrchestrationVersionReview>> GetAll(
        PagedSettings pagedSettings,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Max(1, pagedSettings?.PageNumber ?? 1);
        var pageSize = Math.Clamp(pagedSettings?.PageSize ?? 25, 1, 100);
        var query = _dbContext.Set<OrchestrationVersionReviewEntity>()
            .AsNoTracking()
            .OrderByDescending(x => x.RequestedOnUtc)
            .ThenByDescending(x => x.Id);

        var totalRows = await query.LongCountAsync(cancellationToken);
        var totalPages = totalRows == 0 ? 1 : (int)Math.Ceiling(totalRows / (double)pageSize);
        var rows = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<OrchestrationVersionReview>(
            pageNumber,
            totalPages,
            totalRows,
            pageSize,
            rows.Select(ToDomain).ToArray());
    }

    /// <inheritdoc />
    public async Task<PagedResult<OrchestrationVersionReview>> Search(
        PagedSettings pagedSettings, string search, string sort, bool descending,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Max(1, pagedSettings?.PageNumber ?? 1);
        var pageSize = Math.Clamp(pagedSettings?.PageSize ?? 25, 1, 100);
        var query = from review in _dbContext.Set<OrchestrationVersionReviewEntity>().AsNoTracking()
                    join version in _dbContext.Set<OrchestrationVersionEntity>().AsNoTracking()
                        on review.OrchestrationVersionId equals version.Id into versions
                    from version in versions.DefaultIfEmpty()
                    join definition in _dbContext.Set<OrchestrationDefinitionEntity>().AsNoTracking()
                        on version.OrchestrationDefinitionId equals definition.Id into definitions
                    from definition in definitions.DefaultIfEmpty()
                    select new { Review = review, Version = version, Definition = definition };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var hasOperation = Enum.TryParse<OrchestrationVersionReviewOperation>(term, true, out var operation);
            var hasApproval = Enum.TryParse<OrchestrationVersionReviewApprovalStatus>(term, true, out var approval);
            var hasApplication = Enum.TryParse<OrchestrationVersionReviewApplicationStatus>(term, true, out var application);
            query = query.Where(x => x.Review.RequestedBy.Contains(term)
                || (x.Version != null && x.Version.Version != null && x.Version.Version.Contains(term))
                || (x.Definition != null && (x.Definition.Name.Contains(term) || x.Definition.Key.Contains(term)))
                || (hasOperation && x.Review.Operation == operation)
                || (hasApproval && x.Review.ApprovalStatus == approval)
                || (hasApplication && x.Review.ApplicationStatus == application));
        }

        var totalRows = await query.LongCountAsync(cancellationToken);
        var ordered = (sort, descending) switch
        {
            ("operation", false) => query.OrderBy(x => x.Review.Operation),
            ("operation", true) => query.OrderByDescending(x => x.Review.Operation),
            ("orchestration", false) => query.OrderBy(x => x.Definition.Name),
            ("orchestration", true) => query.OrderByDescending(x => x.Definition.Name),
            ("version", false) => query.OrderBy(x => x.Version.Version),
            ("version", true) => query.OrderByDescending(x => x.Version.Version),
            ("approval", false) => query.OrderBy(x => x.Review.ApprovalStatus),
            ("approval", true) => query.OrderByDescending(x => x.Review.ApprovalStatus),
            ("application", false) => query.OrderBy(x => x.Review.ApplicationStatus),
            ("application", true) => query.OrderByDescending(x => x.Review.ApplicationStatus),
            ("requested", false) => query.OrderBy(x => x.Review.RequestedOnUtc),
            _ => query.OrderByDescending(x => x.Review.RequestedOnUtc)
        };
        var rows = await ordered.ThenByDescending(x => x.Review.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(x => x.Review).ToListAsync(cancellationToken);
        return new PagedResult<OrchestrationVersionReview>(pageNumber,
            totalRows == 0 ? 1 : (int)Math.Ceiling(totalRows / (double)pageSize), totalRows, pageSize,
            rows.Select(ToDomain).ToArray());
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
