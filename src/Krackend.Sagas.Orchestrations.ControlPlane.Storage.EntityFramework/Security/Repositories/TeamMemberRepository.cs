using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Security.Mappings;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Security.Repositories;

/// <summary>
/// Persists and queries team membership data.
/// </summary>
public sealed class TeamMemberRepository : ITeamMemberRepository
{
    private readonly ControlPlaneDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamMemberRepository"/> class.
    /// </summary>
    /// <param name="dbContext">Security db context.</param>
    public TeamMemberRepository(ControlPlaneDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task Add(TeamMember teamMember, CancellationToken cancellationToken = default)
    {
        _dbContext.TeamMembers.Add(teamMember.ToEntity());
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task Remove(Id teamId, string externalUserId, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.TeamMembers.FirstOrDefaultAsync(
            x => x.TeamId == teamId && x.ExternalUserId == externalUserId,
            cancellationToken);

        if (current is null)
        {
            return;
        }

        _dbContext.TeamMembers.Remove(current);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> Exists(Id teamId, string externalUserId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TeamMembers.AnyAsync(
            x => x.TeamId == teamId && x.ExternalUserId == externalUserId,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<TeamMember>> GetByTeam(Id teamId, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.TeamMembers
            .AsNoTracking()
            .Where(x => x.TeamId == teamId)
            .ToListAsync(cancellationToken);

        return rows.Select(x => x.ToDefinition()).ToArray();
    }

    /// <inheritdoc />
    public async Task<PagedResult<TeamMember>> Search(Id teamId, int pageNumber, int pageSize, string searchText, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.TeamMembers.AsNoTracking().Where(x => x.TeamId == teamId);
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim();
            query = query.Where(x => x.ExternalUserId.Contains(term) || (x.DisplayName != null && x.DisplayName.Contains(term)));
        }

        var total = await query.LongCountAsync(cancellationToken);
        var page = Math.Max(1, pageNumber);
        var size = Math.Clamp(pageSize, 1, 100);
        var rows = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.ExternalUserId).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size).ToArrayAsync(cancellationToken);
        return new PagedResult<TeamMember>(page, total == 0 ? 1 : (int)Math.Ceiling(total / (double)size), total, size,
            rows.Select(x => x.ToDefinition()));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Id, int>> CountByTeams(IReadOnlyCollection<Id> teamIds, CancellationToken cancellationToken = default)
    {
        if (teamIds.Count == 0) return new Dictionary<Id, int>();
        var rows = await _dbContext.TeamMembers.AsNoTracking().Where(x => teamIds.Contains(x.TeamId))
            .GroupBy(x => x.TeamId).Select(x => new { Id = x.Key, Count = x.Count() }).ToArrayAsync(cancellationToken);
        return rows.ToDictionary(x => x.Id, x => x.Count);
    }
}
