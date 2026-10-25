using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Mappings;
using Sieve.Services;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Repositories;

/// <summary>
/// Represents OrchestrationVersionRepository.
/// </summary>
public sealed class OrchestrationVersionRepository : IOrchestrationVersionRepository
{
    private readonly ControlPlaneDbContext _dbContext;
    private readonly ISieveProcessor _sieveProcessor;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="dbContext">The dbContext value.</param>
    /// <param name="sieveProcessor">The sieveProcessor value.</param>
    public OrchestrationVersionRepository(ControlPlaneDbContext dbContext, ISieveProcessor sieveProcessor)
    {
        _dbContext = dbContext;
        _sieveProcessor = sieveProcessor;
    }

    /// <summary>
    /// Executes Create.
    /// </summary>
    public async Task Create(OrchestrationVersion orchestrationVersion, CancellationToken cancellationToken = default)
    {
        if (IsOpenDraft(orchestrationVersion.Status)
            && await HasOpenDraft(orchestrationVersion.OrchestrationDefinitionId, cancellationToken))
        {
            throw new InvalidOperationException(
                "This orchestration already has an open draft. Deploy or abandon it before creating another draft.");
        }

        _dbContext.OrchestrationVersions.Add(orchestrationVersion.ToEntity());
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsOpenDraftConstraintViolation(exception))
        {
            throw new InvalidOperationException(
                "This orchestration already has an open draft. Deploy or abandon it before creating another draft.",
                exception);
        }
    }

    /// <summary>
    /// Executes Update.
    /// </summary>
    public async Task Update(OrchestrationVersion orchestrationVersion, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.OrchestrationVersions.FirstOrDefaultAsync(x => x.Id == orchestrationVersion.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"OrchestrationVersion '{orchestrationVersion.Id}' was not found.");

        orchestrationVersion.DraftSlot = GetDraftSlot(
            orchestrationVersion.OrchestrationDefinitionId,
            orchestrationVersion.Id,
            orchestrationVersion.Status);
        var next = orchestrationVersion.ToEntity();
        _dbContext.Entry(current).CurrentValues.SetValues(next);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes SetStatus.
    /// </summary>
    public async Task SetStatus(Id orchestrationVersionId, OrchestrationVersionStatus status, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.OrchestrationVersions.FirstOrDefaultAsync(x => x.Id == orchestrationVersionId, cancellationToken)
            ?? throw new KeyNotFoundException($"OrchestrationVersion '{orchestrationVersionId}' was not found.");

        current.Status = status;
        current.DraftSlot = GetDraftSlot(current.OrchestrationDefinitionId, current.Id, status);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes GetAll.
    /// </summary>
    public async Task<PagedResult<OrchestrationVersion>> GetAll(Id orchestrationDefinitionId, PagedSettings pagedSettings, CancellationToken cancellationToken = default)
        => await Search(orchestrationDefinitionId, pagedSettings, string.Empty, cancellationToken);

    /// <inheritdoc />
    public async Task<PagedResult<OrchestrationVersion>> Search(Id orchestrationDefinitionId, PagedSettings pagedSettings, string search, CancellationToken cancellationToken = default)
    {
        var sieveModel = pagedSettings.ToSieveModel();
        var baseQuery = _dbContext.OrchestrationVersions
            .AsNoTracking()
            .Where(x => x.OrchestrationDefinitionId == orchestrationDefinitionId);

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var matchesDraft = "Draft".Contains(term, StringComparison.OrdinalIgnoreCase);
            baseQuery = baseQuery.Where(x => x.Version.Contains(term) || x.VersionLabel.Contains(term) ||
                (matchesDraft && (x.Version == null || x.Version == string.Empty)));
        }

        var filteredAndSortedQuery = _sieveProcessor.Apply(sieveModel, baseQuery, applyPagination: false);

        var totalRows = await filteredAndSortedQuery.LongCountAsync(cancellationToken);
        var pageNumber = sieveModel.Page ?? 1;
        var pageSize = sieveModel.PageSize ?? 25;
        var totalPages = (int)Math.Ceiling((double)totalRows / pageSize);

        var rows = await _sieveProcessor.Apply(
                sieveModel,
                filteredAndSortedQuery,
                applyFiltering: false,
                applySorting: false,
                applyPagination: true)
            .ToListAsync(cancellationToken);

        return new PagedResult<OrchestrationVersion>(
            pageNumber,
            totalPages,
            totalRows,
            pageSize,
            rows.Select(x => x.ToDefinition()));
    }

    /// <summary>
    /// Executes Exists.
    /// </summary>
    public Task<bool> Exists(Id orchestrationDefinitionId, SemanticVersion version, CancellationToken cancellationToken = default)
    {
        var versionText = version.ToString();

        return _dbContext.OrchestrationVersions
            .AsNoTracking()
            .AnyAsync(
                x => x.OrchestrationDefinitionId == orchestrationDefinitionId
                    && x.Version == versionText
                    && x.Status != OrchestrationVersionStatus.Archived,
                cancellationToken);
    }

    /// <summary>
    /// Executes GetById.
    /// </summary>
    public async Task<OrchestrationVersion> GetById(Id orchestrationVersionId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.OrchestrationVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orchestrationVersionId, cancellationToken)
            ?? throw new KeyNotFoundException($"OrchestrationVersion '{orchestrationVersionId}' was not found.");

        return entity.ToDefinition();
    }

    /// <summary>
    /// Executes GetLatestByOrchestrationDefinitionId.
    /// </summary>
    public async Task<OrchestrationVersion> GetLatestByOrchestrationDefinitionId(
        Id orchestrationDefinitionId,
        Id? excludeOrchestrationVersionId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.OrchestrationVersions
            .AsNoTracking()
            .Where(x => x.OrchestrationDefinitionId == orchestrationDefinitionId
                && x.Status != OrchestrationVersionStatus.Archived);

        if (excludeOrchestrationVersionId.HasValue)
        {
            query = query.Where(x => x.Id != excludeOrchestrationVersionId.Value);
        }

        var entity = await query
            .OrderByDescending(x => x.CreatedOnUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return entity?.ToDefinition();
    }

    private static string GetDraftSlot(Id orchestrationDefinitionId, Id versionId, OrchestrationVersionStatus status)
        => IsOpenDraft(status)
            ? $"open:{orchestrationDefinitionId}"
            : $"closed:{versionId}";

    private Task<bool> HasOpenDraft(Id orchestrationDefinitionId, CancellationToken cancellationToken)
        => _dbContext.OrchestrationVersions.AnyAsync(
            x => x.OrchestrationDefinitionId == orchestrationDefinitionId
                && (x.Status == OrchestrationVersionStatus.Draft
                    || x.Status == OrchestrationVersionStatus.PendingDeployment),
            cancellationToken);

    private static bool IsOpenDraft(OrchestrationVersionStatus status)
        => status is OrchestrationVersionStatus.Draft or OrchestrationVersionStatus.PendingDeployment;

    private static bool IsOpenDraftConstraintViolation(DbUpdateException exception)
        => exception.InnerException?.Message.Contains("DraftSlot", StringComparison.OrdinalIgnoreCase) == true
            || exception.Message.Contains("DraftSlot", StringComparison.OrdinalIgnoreCase);
}
