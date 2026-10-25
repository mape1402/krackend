using Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

/// <summary>
/// Handles get teams query requests.
/// </summary>
public sealed class GetTeamsQueryHandler : IRequestHandler<GetTeamsQuery, ApplicationPagedResult<TeamModel>>
{
    private readonly ITeamRepository _teamRepository;
    private readonly ITeamMemberRepository _teamMemberRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTeamsQueryHandler"/> class.
    /// </summary>
    /// <param name="teamRepository">Team repository dependency.</param>
    /// <param name="teamMemberRepository">Team member repository dependency.</param>
    public GetTeamsQueryHandler(ITeamRepository teamRepository, ITeamMemberRepository teamMemberRepository)
    {
        _teamRepository = teamRepository ?? throw new ArgumentNullException(nameof(teamRepository));
        _teamMemberRepository = teamMemberRepository ?? throw new ArgumentNullException(nameof(teamMemberRepository));
    }

    /// <inheritdoc />
    public async Task<ApplicationPagedResult<TeamModel>> Handle(GetTeamsQuery request, CancellationToken cancellationToken)
    {
        var paged = request.ActiveOnly
            ? await _teamRepository.SearchActive(request.PagedSettings.PageNumber, request.PagedSettings.PageSize,
                request.SearchText, cancellationToken)
            : request.Catalog
            ? await _teamRepository.Search(request.PagedSettings.PageNumber, request.PagedSettings.PageSize,
                request.SearchText, request.Sort, request.Descending, cancellationToken)
            : await _teamRepository.GetAll(request.PagedSettings.PageNumber, request.PagedSettings.PageSize,
                request.SearchText, cancellationToken);

        var counts = request.Catalog || request.ActiveOnly
            ? await _teamMemberRepository.CountByTeams(paged.Rows.Select(team => team.Id).ToArray(), cancellationToken)
            : null;

        var rows = new List<TeamModel>();
        foreach (var team in paged.Rows)
        {
            var count = request.Catalog || request.ActiveOnly
                ? counts.GetValueOrDefault(team.Id)
                : (await _teamMemberRepository.GetByTeam(team.Id, cancellationToken)).Count;
            rows.Add(new TeamModel
            {
                Id = team.Id.ToString(),
                Key = team.Key,
                DisplayName = team.DisplayName,
                Description = team.Description ?? string.Empty,
                IsActive = team.IsActive,
                MemberCount = count,
            });
        }

        return new ApplicationPagedResult<TeamModel>(
            paged.PageNumber,
            paged.TotalPages,
            paged.TotalRows,
            paged.PageSize,
            rows);
    }
}
