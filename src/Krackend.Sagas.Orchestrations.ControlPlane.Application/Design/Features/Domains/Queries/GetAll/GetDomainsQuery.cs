using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Retrieves paged domain catalog entries.
/// </summary>
public sealed record GetDomainsQuery(
    ApplicationPagedSettings PagedSettings,
    string SearchText = "") : IRequest<ApplicationPagedResult<DomainModel>>;
