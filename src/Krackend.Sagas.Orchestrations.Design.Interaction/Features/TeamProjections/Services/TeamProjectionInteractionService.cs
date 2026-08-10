using Krackend.Sagas.Orchestrations.Design.Storage;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Provides interaction operations for team projections.
/// </summary>
public sealed class TeamProjectionInteractionService : ITeamProjectionInteractionService
{
    private readonly ITeamProjectionRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamProjectionInteractionService"/> class.
    /// </summary>
    /// <param name="repository">Team projection repository dependency.</param>
    public TeamProjectionInteractionService(ITeamProjectionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<TeamProjectionModel>> Search(string searchText, bool activeOnly, int take, CancellationToken cancellationToken = default)
    {
        var rows = await _repository.Search(searchText, activeOnly, take, cancellationToken);
        return rows.Select(x => new TeamProjectionModel
        {
            Id = x.Id.ToString(),
            Key = x.Key,
            DisplayName = x.DisplayName,
            IsActive = x.IsActive,
        }).ToArray();
    }
}
