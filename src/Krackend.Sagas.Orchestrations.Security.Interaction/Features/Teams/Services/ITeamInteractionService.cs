namespace Krackend.Sagas.Orchestrations.Security.Interaction;

/// <summary>
/// Defines interaction operations for teams and members.
/// </summary>
public interface ITeamInteractionService
{
    /// <summary>
    /// Creates or updates a team.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Team identifier.</returns>
    Task<string> Upsert(UpsertTeamCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets team active state.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when operation succeeds.</returns>
    Task<bool> SetIsActive(SetTeamIsActiveCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds one member to a team.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when operation succeeds.</returns>
    Task<bool> AddMember(AddTeamMemberCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes one member from a team.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when operation succeeds.</returns>
    Task<bool> RemoveMember(RemoveTeamMemberCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paged teams.
    /// </summary>
    /// <param name="query">Query to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged teams.</returns>
    Task<InteractionPagedResult<TeamModel>> GetAll(GetTeamsQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets team members.
    /// </summary>
    /// <param name="query">Query to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Team members.</returns>
    Task<IReadOnlyCollection<TeamMemberModel>> GetMembers(GetTeamMembersQuery query, CancellationToken cancellationToken = default);
}
