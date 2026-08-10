using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Mappings;
using Sieve.Services;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Repositories;

/// <summary>
/// Represents OrchestrationDefinitionRepository.
/// </summary>
public sealed class OrchestrationDefinitionRepository : IOrchestrationDefinitionRepository
{
    private readonly DesignStorageDbContext _dbContext;
    private readonly ISieveProcessor _sieveProcessor;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="dbContext">The dbContext value.</param>
    /// <param name="sieveProcessor">The sieveProcessor value.</param>
    public OrchestrationDefinitionRepository(DesignStorageDbContext dbContext, ISieveProcessor sieveProcessor)
    {
        _dbContext = dbContext;
        _sieveProcessor = sieveProcessor;
    }

    /// <summary>
    /// Executes Create.
    /// </summary>
    public async Task Create(OrchestrationDefinition orchestrationDefinition, CancellationToken cancellationToken = default)
    {
        _dbContext.OrchestrationDefinitions.Add(orchestrationDefinition.ToEntity());
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes Update.
    /// </summary>
    public async Task Update(OrchestrationDefinition orchestrationDefinition, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.OrchestrationDefinitions.FirstOrDefaultAsync(x => x.Id == orchestrationDefinition.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"OrchestrationDefinition '{orchestrationDefinition.Id}' was not found.");

        var next = orchestrationDefinition.ToEntity();
        _dbContext.Entry(current).CurrentValues.SetValues(next);
        current.Tags = next.Tags;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes SetIsActive.
    /// </summary>
    public async Task SetIsActive(Id orchestrationDefinitionId, bool isActive, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.OrchestrationDefinitions.FirstOrDefaultAsync(x => x.Id == orchestrationDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"OrchestrationDefinition '{orchestrationDefinitionId}' was not found.");

        current.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes GetAll.
    /// </summary>
    public async Task<PagedResult<OrchestrationDefinition>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default)
    {
        var sieveModel = pagedSettings.ToSieveModel();
        var baseQuery = _dbContext.OrchestrationDefinitions
            .AsNoTracking()
            .Include(x => x.DomainRef)
            .Include(x => x.OwnerTeamRef)
            .AsQueryable();
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

        return new PagedResult<OrchestrationDefinition>(
            pageNumber,
            totalPages,
            totalRows,
            pageSize,
            rows.Select(x => x.ToDefinition()));
    }

    /// <summary>
    /// Executes GetById.
    /// </summary>
    public async Task<OrchestrationDefinition> GetById(Id orchestrationDefinitionId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.OrchestrationDefinitions
            .AsNoTracking()
            .Include(x => x.DomainRef)
            .Include(x => x.OwnerTeamRef)
            .FirstOrDefaultAsync(x => x.Id == orchestrationDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"OrchestrationDefinition '{orchestrationDefinitionId}' was not found.");

        return entity.ToDefinition();
    }
}
