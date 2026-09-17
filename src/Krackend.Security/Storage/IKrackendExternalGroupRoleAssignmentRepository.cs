using Krackend.Security.Core;

namespace Krackend.Security.Storage;

/// <summary>
/// Persists and queries external group role assignments.
/// </summary>
public interface IKrackendExternalGroupRoleAssignmentRepository
{
    /// <summary>
    /// Adds or updates an external group role assignment.
    /// </summary>
    /// <param name="assignment">External group role assignment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Upsert(KrackendExternalGroupRoleAssignment assignment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an external group role assignment.
    /// </summary>
    /// <param name="assignmentId">Assignment identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Remove(string assignmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets external group role assignments for the supplied groups.
    /// </summary>
    /// <param name="provider">Provider name.</param>
    /// <param name="externalGroupIds">External group identifiers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Enabled group role assignments.</returns>
    Task<IReadOnlyCollection<KrackendExternalGroupRoleAssignment>> GetForGroups(string provider, IEnumerable<string> externalGroupIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all external group role assignments.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Enabled group role assignments.</returns>
    Task<IReadOnlyCollection<KrackendExternalGroupRoleAssignment>> GetAll(CancellationToken cancellationToken = default);
}
