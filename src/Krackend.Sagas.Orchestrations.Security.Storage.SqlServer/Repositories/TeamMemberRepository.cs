using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Security.Core;
using Krackend.Sagas.Orchestrations.Security.Storage;
using Krackend.Sagas.Orchestrations.Security.Storage.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Security.Storage.SqlServer.Mappings;

namespace Krackend.Sagas.Orchestrations.Security.Storage.SqlServer.Repositories;

/// <summary>
/// Persists and queries team membership data.
/// </summary>
public sealed class TeamMemberRepository : ITeamMemberRepository
{
    private readonly SecurityStorageDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamMemberRepository"/> class.
    /// </summary>
    /// <param name="dbContext">Security db context.</param>
    public TeamMemberRepository(SecurityStorageDbContext dbContext)
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
}
