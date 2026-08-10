using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Security.Interaction;

/// <summary>
/// Retrieves team members by team id.
/// </summary>
public sealed record GetTeamMembersQuery(string TeamId) : IRequest<IReadOnlyCollection<TeamMemberModel>>;
