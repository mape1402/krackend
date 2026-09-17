using Krackend.Security.Core;

namespace Krackend.Security.Storage;

/// <summary>
/// Persists and queries product subjects.
/// </summary>
public interface IKrackendSubjectRepository
{
    /// <summary>
    /// Upserts a known subject.
    /// </summary>
    /// <param name="subject">Subject to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Upsert(KrackendSubject subject, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets whether a subject is enabled.
    /// </summary>
    /// <param name="subjectId">Product subject identifier.</param>
    /// <param name="isEnabled">Enabled state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetEnabled(string subjectId, bool isEnabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a subject by product identifier.
    /// </summary>
    /// <param name="subjectId">Product subject identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Subject or null.</returns>
    Task<KrackendSubject> GetById(string subjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a subject by provider and provider-specific identifier.
    /// </summary>
    /// <param name="provider">Provider name.</param>
    /// <param name="subjectId">Provider-specific subject identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Subject or null.</returns>
    Task<KrackendSubject> GetByExternalSubject(string provider, string subjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paged list of subjects.
    /// </summary>
    /// <param name="pageNumber">Page number.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="searchText">Optional search text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged subjects.</returns>
    Task<KrackendPagedResult<KrackendSubject>> GetAll(int pageNumber, int pageSize, string searchText = "", CancellationToken cancellationToken = default);
}
