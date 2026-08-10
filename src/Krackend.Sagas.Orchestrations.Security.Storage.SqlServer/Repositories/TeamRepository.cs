using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Security.Core;
using Krackend.Sagas.Orchestrations.Security.Storage;
using Krackend.Sagas.Orchestrations.Security.Storage.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Security.Storage.SqlServer.Mappings;

namespace Krackend.Sagas.Orchestrations.Security.Storage.SqlServer.Repositories;

/// <summary>
/// Persists and queries team data.
/// </summary>
public sealed class TeamRepository : ITeamRepository
{
    private readonly SecurityStorageDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamRepository"/> class.
    /// </summary>
    /// <param name="dbContext">Security db context.</param>
    public TeamRepository(SecurityStorageDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task Upsert(Team team, CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.Teams.AnyAsync(x => x.Id == team.Id, cancellationToken);
        if (!exists)
        {
            _dbContext.Teams.Add(team.ToEntity());
        }
        else
        {
            var current = await _dbContext.Teams.FirstAsync(x => x.Id == team.Id, cancellationToken);
            var next = team.ToEntity();
            _dbContext.Entry(current).CurrentValues.SetValues(next);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetIsActive(Id teamId, bool isActive, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.Teams.FirstOrDefaultAsync(x => x.Id == teamId, cancellationToken)
            ?? throw new KeyNotFoundException($"Team '{teamId}' was not found.");

        current.IsActive = isActive;
        current.UpdatedOnUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Team> GetById(Id teamId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Teams.AsNoTracking().FirstOrDefaultAsync(x => x.Id == teamId, cancellationToken)
            ?? throw new KeyNotFoundException($"Team '{teamId}' was not found.");

        return entity.ToDefinition();
    }

    /// <inheritdoc />
    public async Task<Team> GetByKey(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var entity = await _dbContext.Teams.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        return entity?.ToDefinition();
    }

    /// <inheritdoc />
    public async Task<PagedResult<Team>> GetAll(int pageNumber, int pageSize, string searchText = "", CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Teams.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim();
            query = query.Where(x =>
                EF.Functions.Like(x.Key, $"%{term}%")
                || EF.Functions.Like(x.DisplayName, $"%{term}%")
                || EF.Functions.Like(x.Description, $"%{term}%"));
        }

        var totalRows = await query.LongCountAsync(cancellationToken);
        var safePageSize = pageSize <= 0 ? 25 : pageSize;
        var safePageNumber = pageNumber <= 0 ? 1 : pageNumber;
        var totalPages = (int)Math.Ceiling((double)totalRows / safePageSize);

        var rows = await query
            .OrderBy(x => x.DisplayName)
            .Skip((safePageNumber - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Team>(
            safePageNumber,
            totalPages,
            totalRows,
            safePageSize,
            rows.Select(x => x.ToDefinition()));
    }
}
