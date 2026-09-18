using Krackend.Sagas.Orchestrations.Security.Core;

namespace Krackend.Sagas.Orchestrations.Security.Storage;

/// <summary>
/// Persists and queries direct permission assignments.
/// </summary>
public interface IKrackendPermissionAssignmentRepository
{
    /// <summary>
    /// Adds or updates a permission assignment.
    /// </summary>
    /// <param name="assignment">Permission assignment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Upsert(KrackendPermissionAssignment assignment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a permission assignment.
    /// </summary>
    /// <param name="assignmentId">Assignment identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Remove(string assignmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets direct permission assignments for an external subject.
    /// </summary>
    /// <param name="provider">Provider name.</param>
    /// <param name="subjectId">Provider-specific subject identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Enabled permission assignments.</returns>
    Task<IReadOnlyCollection<KrackendPermissionAssignment>> GetForSubject(string provider, string subjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets direct permission assignments for a product subject.
    /// </summary>
    /// <param name="subject">Known subject.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Enabled permission assignments.</returns>
    Task<IReadOnlyCollection<KrackendPermissionAssignment>> GetForSubject(KrackendSubject subject, CancellationToken cancellationToken = default);
}
