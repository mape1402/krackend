using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Security.Interaction;

/// <summary>
/// Retrieves paged teams.
/// </summary>
public sealed record GetTeamsQuery(
    InteractionPagedSettings PagedSettings,
    string SearchText = "") : IRequest<InteractionPagedResult<TeamModel>>;
