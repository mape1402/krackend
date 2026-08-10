using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Security.Interaction;

/// <summary>
/// Sets active state for a team.
/// </summary>
public sealed record SetTeamIsActiveCommand(
    string TeamId,
    bool IsActive,
    string Actor) : IRequest<bool>;
