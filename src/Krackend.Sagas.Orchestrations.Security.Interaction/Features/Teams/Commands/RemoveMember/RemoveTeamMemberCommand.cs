using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Security.Interaction;

/// <summary>
/// Removes one member from a team.
/// </summary>
public sealed record RemoveTeamMemberCommand(
    string TeamId,
    string ExternalUserId) : IRequest<bool>;
