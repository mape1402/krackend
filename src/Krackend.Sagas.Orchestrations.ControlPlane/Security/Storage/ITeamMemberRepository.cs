namespace Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Core;

/// <summary>
/// Defines persistence operations for team members.
/// </summary>
public interface ITeamMemberRepository
{
    /// <summary>
    /// Adds a member to a team.
    /// </summary>
    /// <param name="teamMember">Membership to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Add(TeamMember teamMember, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a member from a team by external identity.
    /// </summary>
    /// <param name="teamId">Team identifier.</param>
    /// <param name="externalUserId">External user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Remove(Id teamId, string externalUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a member already exists.
    /// </summary>
    /// <param name="teamId">Team identifier.</param>
    /// <param name="externalUserId">External user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when member exists.</returns>
    Task<bool> Exists(Id teamId, string externalUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets members by team.
    /// </summary>
    /// <param name="teamId">Team identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Team members.</returns>
    Task<IReadOnlyCollection<TeamMember>> GetByTeam(Id teamId, CancellationToken cancellationToken = default);
}
