using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Security.Interaction;

/// <summary>
/// Creates or updates a team.
/// </summary>
public sealed record UpsertTeamCommand(
    string TeamId,
    string Key,
    string DisplayName,
    string Description,
    string Actor) : IRequest<string>;
