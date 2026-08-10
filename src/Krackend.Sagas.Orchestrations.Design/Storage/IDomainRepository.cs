namespace Krackend.Sagas.Orchestrations.Design.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;

/// <summary>
/// Defines persistence operations for domain catalog entries.
/// </summary>
public interface IDomainRepository
{
    /// <summary>
    /// Creates or updates a domain entry.
    /// </summary>
    /// <param name="domain">Domain entry to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Upsert(Domain domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets active flag for a domain.
    /// </summary>
    /// <param name="domainId">Domain identifier.</param>
    /// <param name="isActive">New active flag value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetIsActive(Id domainId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one domain by identifier.
    /// </summary>
    /// <param name="domainId">Domain identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Domain entry.</returns>
    Task<Domain> GetById(Id domainId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one domain by key.
    /// </summary>
    /// <param name="key">Domain key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Domain entry or null when not found.</returns>
    Task<Domain> GetByKey(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns paged domains optionally filtered by search text.
    /// </summary>
    /// <param name="pagedSettings">Paging settings.</param>
    /// <param name="searchText">Optional search text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged result.</returns>
    Task<PagedResult<Domain>> GetAll(PagedSettings pagedSettings, string searchText = "", CancellationToken cancellationToken = default);
}
