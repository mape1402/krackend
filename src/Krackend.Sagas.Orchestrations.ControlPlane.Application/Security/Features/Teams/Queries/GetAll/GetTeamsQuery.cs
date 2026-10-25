using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

/// <summary>
/// Retrieves paged teams.
/// </summary>
public sealed record GetTeamsQuery(
    ApplicationPagedSettings PagedSettings,
    string SearchText = "",
    bool Catalog = false,
    string Sort = "",
    bool Descending = false) : IRequest<ApplicationPagedResult<TeamModel>>;
