using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

/// <summary>
/// Creates or updates a team.
/// </summary>
public sealed record UpsertTeamCommand(
    string TeamId,
    string Key,
    string DisplayName,
    string Description,
    string Actor) : IRequest<string>;
