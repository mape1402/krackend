using Krackend.Sagas.Orchestrations.Security.Core;
using Krackend.Sagas.Orchestrations.Security.Storage;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Security.Storage.EntityFramework.Repositories;

/// <summary>
/// Entity Framework repository for external group role assignments.
/// </summary>
public sealed class EntityFrameworkKrackendExternalGroupRoleAssignmentRepository : IKrackendExternalGroupRoleAssignmentRepository
{
    private readonly KrackendSecurityDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkKrackendExternalGroupRoleAssignmentRepository"/> class.
    /// </summary>
    /// <param name="dbContext">Security database context.</param>
    public EntityFrameworkKrackendExternalGroupRoleAssignmentRepository(KrackendSecurityDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task Upsert(KrackendExternalGroupRoleAssignment assignment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        Normalize(assignment);

        var current = await _dbContext.ExternalGroupRoleAssignments.FirstOrDefaultAsync(x => x.Id == assignment.Id, cancellationToken)
            ?? await _dbContext.ExternalGroupRoleAssignments.FirstOrDefaultAsync(x =>
                x.Provider == assignment.Provider &&
                x.ExternalGroupId == assignment.ExternalGroupId &&
                x.Role == assignment.Role &&
                x.ScopeType == assignment.ScopeType &&
                x.ScopeId == assignment.ScopeId,
                cancellationToken);

        if (current is null)
        {
            if (string.IsNullOrWhiteSpace(assignment.Id))
            {
                assignment.Id = NewId();
            }

            assignment.CreatedAtUtc = assignment.CreatedAtUtc == default ? DateTime.UtcNow : assignment.CreatedAtUtc;
            _dbContext.ExternalGroupRoleAssignments.Add(assignment);
        }
        else
        {
            current.Source = assignment.Source;
            current.IsEnabled = assignment.IsEnabled;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task Remove(string assignmentId, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.ExternalGroupRoleAssignments.FirstOrDefaultAsync(x => x.Id == assignmentId, cancellationToken);
        if (current is null)
        {
            return;
        }

        _dbContext.ExternalGroupRoleAssignments.Remove(current);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<KrackendExternalGroupRoleAssignment>> GetForGroups(string provider, IEnumerable<string> externalGroupIds, CancellationToken cancellationToken = default)
    {
        var normalizedProvider = Normalize(provider);
        var groups = externalGroupIds?
            .Select(Normalize)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (groups.Length == 0)
        {
            return [];
        }

        return await _dbContext.ExternalGroupRoleAssignments.AsNoTracking()
            .Where(x => x.Provider == normalizedProvider && x.IsEnabled && groups.Contains(x.ExternalGroupId))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<KrackendExternalGroupRoleAssignment>> GetAll(CancellationToken cancellationToken = default)
        => await _dbContext.ExternalGroupRoleAssignments.AsNoTracking()
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Provider)
            .ThenBy(x => x.ExternalGroupId)
            .ThenBy(x => x.Role)
            .ToListAsync(cancellationToken);

    private static void Normalize(KrackendExternalGroupRoleAssignment assignment)
    {
        assignment.Provider = Normalize(assignment.Provider);
        assignment.ExternalGroupId = Normalize(assignment.ExternalGroupId);
        assignment.Role = Normalize(assignment.Role);
        assignment.ScopeType = Normalize(assignment.ScopeType);
        assignment.ScopeId = Normalize(assignment.ScopeId);
        assignment.Source = Normalize(assignment.Source);
    }

    private static string Normalize(string value)
        => value?.Trim() ?? string.Empty;

    private static string NewId()
        => Guid.NewGuid().ToString("N");
}
