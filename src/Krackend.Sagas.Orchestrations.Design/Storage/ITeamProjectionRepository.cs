namespace Krackend.Sagas.Orchestrations.Design.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;

/// <summary>
/// Defines persistence operations for Design team projections.
/// </summary>
public interface ITeamProjectionRepository
{
    /// <summary>
    /// Creates or updates a team projection entry.
    /// </summary>
    /// <param name="team">Projection model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Upsert(TeamProjection team, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one team projection by id.
    /// </summary>
    /// <param name="teamId">Team id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Team projection or null.</returns>
    Task<TeamProjection> GetById(Id teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches team projections by key or display name.
    /// </summary>
    /// <param name="searchText">Search text.</param>
    /// <param name="activeOnly">Whether to return only active rows.</param>
    /// <param name="take">Maximum number of rows.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching projections.</returns>
    Task<IReadOnlyCollection<TeamProjection>> Search(string searchText, bool activeOnly, int take, CancellationToken cancellationToken = default);
}
