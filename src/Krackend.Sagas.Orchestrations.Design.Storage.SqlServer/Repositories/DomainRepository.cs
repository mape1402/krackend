using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Mappings;
using Sieve.Services;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Repositories;

/// <summary>
/// Persists and queries domain catalog entries.
/// </summary>
public sealed class DomainRepository : IDomainRepository
{
    private readonly DesignStorageDbContext _dbContext;
    private readonly ISieveProcessor _sieveProcessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainRepository"/> class.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    /// <param name="sieveProcessor">Sieve processor dependency.</param>
    public DomainRepository(DesignStorageDbContext dbContext, ISieveProcessor sieveProcessor)
    {
        _dbContext = dbContext;
        _sieveProcessor = sieveProcessor;
    }

    /// <inheritdoc />
    public async Task Upsert(Domain domain, CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.Domains.AnyAsync(x => x.Id == domain.Id, cancellationToken);
        if (!exists)
        {
            _dbContext.Domains.Add(domain.ToEntity());
        }
        else
        {
            var current = await _dbContext.Domains.FirstAsync(x => x.Id == domain.Id, cancellationToken);
            var next = domain.ToEntity();
            _dbContext.Entry(current).CurrentValues.SetValues(next);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetIsActive(Id domainId, bool isActive, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.Domains.FirstOrDefaultAsync(x => x.Id == domainId, cancellationToken)
            ?? throw new KeyNotFoundException($"Domain '{domainId}' was not found.");

        current.IsActive = isActive;
        current.UpdatedOnUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Domain> GetById(Id domainId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Domains.AsNoTracking().FirstOrDefaultAsync(x => x.Id == domainId, cancellationToken)
            ?? throw new KeyNotFoundException($"Domain '{domainId}' was not found.");

        return entity.ToDefinition();
    }

    /// <inheritdoc />
    public async Task<Domain> GetByKey(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var entity = await _dbContext.Domains.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, cancellationToken);

        return entity?.ToDefinition();
    }

    /// <inheritdoc />
    public async Task<PagedResult<Domain>> GetAll(PagedSettings pagedSettings, string searchText = "", CancellationToken cancellationToken = default)
    {
        var sieveModel = pagedSettings.ToSieveModel();
        var query = _dbContext.Domains.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim();
            query = query.Where(x =>
                EF.Functions.Like(x.Key, $"%{term}%")
                || EF.Functions.Like(x.DisplayName, $"%{term}%")
                || EF.Functions.Like(x.Description, $"%{term}%"));
        }

        var filteredAndSortedQuery = _sieveProcessor.Apply(sieveModel, query, applyPagination: false);
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

        return new PagedResult<Domain>(
            pageNumber,
            totalPages,
            totalRows,
            pageSize,
            rows.Select(x => x.ToDefinition()));
    }
}
