using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Retrieves paged domain catalog entries.
/// </summary>
public sealed record GetDomainsQuery(
    InteractionPagedSettings PagedSettings,
    string SearchText = "") : IRequest<InteractionPagedResult<DomainModel>>;
