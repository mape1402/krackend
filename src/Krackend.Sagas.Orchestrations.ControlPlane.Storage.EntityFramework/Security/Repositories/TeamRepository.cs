using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Security.Mappings;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Security.Repositories;

/// <summary>
/// Persists and queries team data.
/// </summary>
public sealed class TeamRepository : ITeamRepository
{
    private readonly ControlPlaneDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamRepository"/> class.
    /// </summary>
    /// <param name="dbContext">Security db context.</param>
    public TeamRepository(ControlPlaneDbContext dbContext)
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

    /// <inheritdoc />
    public Task<PagedResult<Team>> Search(int pageNumber, int pageSize, string searchText, string sort, bool descending, CancellationToken cancellationToken = default)
        => Search(pageNumber, pageSize, searchText, sort, descending, false, cancellationToken);

    /// <inheritdoc />
    public Task<PagedResult<Team>> SearchActive(int pageNumber, int pageSize, string searchText, CancellationToken cancellationToken = default)
        => Search(pageNumber, pageSize, searchText, "", false, true, cancellationToken);

    private async Task<PagedResult<Team>> Search(int pageNumber, int pageSize, string searchText, string sort, bool descending, bool activeOnly, CancellationToken cancellationToken)
    {
        var query = _dbContext.Teams.AsNoTracking();
        if (activeOnly)
            query = query.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim();
            query = query.Where(x => x.Key.Contains(term) || x.DisplayName.Contains(term) || (x.Description != null && x.Description.Contains(term)));
        }
        var total = await query.LongCountAsync(cancellationToken);
        var page = Math.Max(1, pageNumber);
        var size = Math.Clamp(pageSize, 1, 100);
        var ordered = (sort, descending) switch
        {
            ("key", false) => query.OrderBy(x => x.Key),
            ("key", true) => query.OrderByDescending(x => x.Key),
            ("status", false) => query.OrderBy(x => x.IsActive),
            ("status", true) => query.OrderByDescending(x => x.IsActive),
            (_, true) => query.OrderByDescending(x => x.DisplayName),
            _ => query.OrderBy(x => x.DisplayName)
        };
        var rows = await ordered.ThenBy(x => x.Id).Skip((page - 1) * size).Take(size).ToArrayAsync(cancellationToken);
        return new PagedResult<Team>(page, total == 0 ? 1 : (int)Math.Ceiling(total / (double)size), total, size, rows.Select(x => x.ToDefinition()));
    }
}
