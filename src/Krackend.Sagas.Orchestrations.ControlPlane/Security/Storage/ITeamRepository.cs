namespace Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Core;

/// <summary>
/// Defines persistence operations for teams.
/// </summary>
public interface ITeamRepository
{
    /// <summary>
    /// Creates or updates a team.
    /// </summary>
    /// <param name="team">Team to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Upsert(Team team, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets team active state.
    /// </summary>
    /// <param name="teamId">Team identifier.</param>
    /// <param name="isActive">New active value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetIsActive(Id teamId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one team by identifier.
    /// </summary>
    /// <param name="teamId">Team identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Team instance.</returns>
    Task<Team> GetById(Id teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one team by key.
    /// </summary>
    /// <param name="key">Team key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Team instance or null.</returns>
    Task<Team> GetByKey(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paged teams optionally filtered by search text.
    /// </summary>
    /// <param name="pageNumber">Page number.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="searchText">Optional search text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged teams.</returns>
    Task<PagedResult<Team>> GetAll(int pageNumber, int pageSize, string searchText = "", CancellationToken cancellationToken = default);
}
