using Krackend.Security.Core;

namespace Krackend.Security.Storage;

/// <summary>
/// Persists and queries direct role assignments.
/// </summary>
public interface IKrackendRoleAssignmentRepository
{
    /// <summary>
    /// Adds or updates a role assignment.
    /// </summary>
    /// <param name="assignment">Role assignment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Upsert(KrackendRoleAssignment assignment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a role assignment.
    /// </summary>
    /// <param name="assignmentId">Assignment identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Remove(string assignmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets direct role assignments for an external subject.
    /// </summary>
    /// <param name="provider">Provider name.</param>
    /// <param name="subjectId">Provider-specific subject identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Enabled role assignments.</returns>
    Task<IReadOnlyCollection<KrackendRoleAssignment>> GetForSubject(string provider, string subjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets direct role assignments for a product subject.
    /// </summary>
    /// <param name="subject">Known subject.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Enabled role assignments.</returns>
    Task<IReadOnlyCollection<KrackendRoleAssignment>> GetForSubject(KrackendSubject subject, CancellationToken cancellationToken = default);
}
