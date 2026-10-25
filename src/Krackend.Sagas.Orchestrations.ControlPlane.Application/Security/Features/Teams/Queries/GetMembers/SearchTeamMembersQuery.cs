using Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

/// <summary>Requests a filtered page of members belonging to a team.</summary>
public sealed record SearchTeamMembersQuery(string TeamId, int PageNumber, int PageSize, string SearchText)
    : IRequest<ApplicationPagedResult<TeamMemberModel>>;

/// <summary>Queries the backing store for a page of team members.</summary>
public sealed class SearchTeamMembersQueryHandler(ITeamMemberRepository repository)
    : IRequestHandler<SearchTeamMembersQuery, ApplicationPagedResult<TeamMemberModel>>
{
    /// <inheritdoc />
    public async Task<ApplicationPagedResult<TeamMemberModel>> Handle(SearchTeamMembersQuery request, CancellationToken cancellationToken)
    {
        var page = await repository.Search(PrimitiveParser.ParseId(request.TeamId), request.PageNumber, request.PageSize, request.SearchText, cancellationToken);
        return new ApplicationPagedResult<TeamMemberModel>(page.PageNumber, page.TotalPages, page.TotalRows, page.PageSize,
            page.Rows.Select(x => new TeamMemberModel
            {
                Id = x.Id.ToString(), TeamId = x.TeamId.ToString(), ExternalUserId = x.ExternalUserId,
                DisplayName = x.DisplayName ?? string.Empty, CreatedOnUtc = x.CreatedOnUtc.ToString("O")
            }).ToArray());
    }
}
