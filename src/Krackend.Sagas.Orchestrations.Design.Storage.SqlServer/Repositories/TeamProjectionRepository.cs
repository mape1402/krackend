using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Mappings;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Repositories;

/// <summary>
/// Persists and queries Design-local team projections.
/// </summary>
public sealed class TeamProjectionRepository : ITeamProjectionRepository
{
    private readonly DesignStorageDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamProjectionRepository"/> class.
    /// </summary>
    /// <param name="dbContext">Design storage db context.</param>
    public TeamProjectionRepository(DesignStorageDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task Upsert(TeamProjection team, CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.TeamProjections.AnyAsync(x => x.Id == team.Id, cancellationToken);
        if (!exists)
        {
            _dbContext.TeamProjections.Add(team.ToEntity());
        }
        else
        {
            var current = await _dbContext.TeamProjections.FirstAsync(x => x.Id == team.Id, cancellationToken);
            var next = team.ToEntity();
            _dbContext.Entry(current).CurrentValues.SetValues(next);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TeamProjection> GetById(Id teamId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.TeamProjections.AsNoTracking().FirstOrDefaultAsync(x => x.Id == teamId, cancellationToken);
        return entity?.ToDefinition();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<TeamProjection>> Search(string searchText, bool activeOnly, int take, CancellationToken cancellationToken = default)
    {
        var safeTake = take <= 0 ? 20 : take;
        var query = _dbContext.TeamProjections.AsNoTracking().AsQueryable();

        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim();
            query = query.Where(x =>
                EF.Functions.Like(x.Key, $"%{term}%")
                || EF.Functions.Like(x.DisplayName, $"%{term}%"));
        }

        var rows = await query
            .OrderBy(x => x.DisplayName)
            .Take(safeTake)
            .ToListAsync(cancellationToken);

        return rows.Select(x => x.ToDefinition()).ToArray();
    }
}
