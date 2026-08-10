namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Defines interaction operations for team projections consumed by Design UI.
/// </summary>
public interface ITeamProjectionInteractionService
{
    /// <summary>
    /// Searches team projections by key or display name.
    /// </summary>
    /// <param name="searchText">Search text.</param>
    /// <param name="activeOnly">Whether to return only active teams.</param>
    /// <param name="take">Maximum number of rows.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching team projections.</returns>
    Task<IReadOnlyCollection<TeamProjectionModel>> Search(string searchText, bool activeOnly, int take, CancellationToken cancellationToken = default);
}
