namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Defines interaction operations for domain catalog entries.
/// </summary>
public interface IDomainInteractionService
{
    /// <summary>
    /// Creates or updates a domain entry.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Persisted domain identifier.</returns>
    Task<string> Upsert(UpsertDomainCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates active state for a domain.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when operation succeeds.</returns>
    Task<bool> SetIsActive(SetDomainIsActiveCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one domain by identifier.
    /// </summary>
    /// <param name="query">Query to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Domain model.</returns>
    Task<DomainModel> GetById(GetDomainByIdQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paged domain entries.
    /// </summary>
    /// <param name="query">Query to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged result.</returns>
    Task<InteractionPagedResult<DomainModel>> GetAll(GetDomainsQuery query, CancellationToken cancellationToken = default);
}
