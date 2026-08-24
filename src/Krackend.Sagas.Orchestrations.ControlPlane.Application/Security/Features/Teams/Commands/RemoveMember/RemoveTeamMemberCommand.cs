using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

/// <summary>
/// Removes one member from a team.
/// </summary>
public sealed record RemoveTeamMemberCommand(
    string TeamId,
    string ExternalUserId) : IRequest<bool>;
