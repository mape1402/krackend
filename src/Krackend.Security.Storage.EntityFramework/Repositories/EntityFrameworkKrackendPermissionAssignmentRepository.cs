using Krackend.Security.Core;
using Krackend.Security.Storage;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Security.Storage.EntityFramework.Repositories;

/// <summary>
/// Entity Framework repository for direct permission assignments.
/// </summary>
public sealed class EntityFrameworkKrackendPermissionAssignmentRepository : IKrackendPermissionAssignmentRepository
{
    private readonly KrackendSecurityDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkKrackendPermissionAssignmentRepository"/> class.
    /// </summary>
    /// <param name="dbContext">Security database context.</param>
    public EntityFrameworkKrackendPermissionAssignmentRepository(KrackendSecurityDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task Upsert(KrackendPermissionAssignment assignment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        Normalize(assignment);

        var current = await _dbContext.PermissionAssignments.FirstOrDefaultAsync(x => x.Id == assignment.Id, cancellationToken)
            ?? await _dbContext.PermissionAssignments.FirstOrDefaultAsync(x =>
                x.Provider == assignment.Provider &&
                x.SubjectId == assignment.SubjectId &&
                x.Permission == assignment.Permission &&
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
            _dbContext.PermissionAssignments.Add(assignment);
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
        var current = await _dbContext.PermissionAssignments.FirstOrDefaultAsync(x => x.Id == assignmentId, cancellationToken);
        if (current is null)
        {
            return;
        }

        _dbContext.PermissionAssignments.Remove(current);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<KrackendPermissionAssignment>> GetForSubject(string provider, string subjectId, CancellationToken cancellationToken = default)
    {
        var normalizedProvider = Normalize(provider);
        var normalizedSubjectId = Normalize(subjectId);
        return GetForSubjectInternal(normalizedProvider, normalizedSubjectId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<KrackendPermissionAssignment>> GetForSubject(KrackendSubject subject, CancellationToken cancellationToken = default)
        => subject is null
            ? Task.FromResult<IReadOnlyCollection<KrackendPermissionAssignment>>([])
            : GetForSubjectInternal(Normalize(subject.Provider), Normalize(subject.SubjectId), cancellationToken);

    private async Task<IReadOnlyCollection<KrackendPermissionAssignment>> GetForSubjectInternal(string provider, string subjectId, CancellationToken cancellationToken)
        => await _dbContext.PermissionAssignments.AsNoTracking()
            .Where(x => x.Provider == provider && x.SubjectId == subjectId && x.IsEnabled)
            .ToListAsync(cancellationToken);

    private static void Normalize(KrackendPermissionAssignment assignment)
    {
        assignment.Provider = Normalize(assignment.Provider);
        assignment.SubjectId = Normalize(assignment.SubjectId);
        assignment.Permission = Normalize(assignment.Permission);
        assignment.ScopeType = Normalize(assignment.ScopeType);
        assignment.ScopeId = Normalize(assignment.ScopeId);
        assignment.Source = Normalize(assignment.Source);
    }

    private static string Normalize(string value)
        => value?.Trim() ?? string.Empty;

    private static string NewId()
        => Guid.NewGuid().ToString("N");
}
