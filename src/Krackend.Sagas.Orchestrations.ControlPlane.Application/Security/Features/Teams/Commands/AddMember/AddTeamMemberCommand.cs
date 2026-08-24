using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

/// <summary>
/// Adds one member to a team.
/// </summary>
public sealed record AddTeamMemberCommand(
    string TeamId,
    string ExternalUserId,
    string DisplayName) : IRequest<bool>;
