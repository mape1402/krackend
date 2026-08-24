using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

/// <summary>
/// Retrieves team members by team id.
/// </summary>
public sealed record GetTeamMembersQuery(string TeamId) : IRequest<IReadOnlyCollection<TeamMemberModel>>;
