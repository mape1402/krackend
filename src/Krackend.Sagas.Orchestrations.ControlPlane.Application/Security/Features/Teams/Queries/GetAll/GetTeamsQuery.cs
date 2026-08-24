using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

/// <summary>
/// Retrieves paged teams.
/// </summary>
public sealed record GetTeamsQuery(
    ApplicationPagedSettings PagedSettings,
    string SearchText = "") : IRequest<ApplicationPagedResult<TeamModel>>;
