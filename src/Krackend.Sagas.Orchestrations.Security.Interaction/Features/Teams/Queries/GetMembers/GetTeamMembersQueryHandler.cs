using Krackend.Sagas.Orchestrations.Security.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Security.Interaction;

/// <summary>
/// Handles get team members query requests.
/// </summary>
public sealed class GetTeamMembersQueryHandler : IRequestHandler<GetTeamMembersQuery, IReadOnlyCollection<TeamMemberModel>>
{
    private readonly ITeamMemberRepository _memberRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTeamMembersQueryHandler"/> class.
    /// </summary>
    /// <param name="memberRepository">Team member repository dependency.</param>
    public GetTeamMembersQueryHandler(ITeamMemberRepository memberRepository)
    {
        _memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<TeamMemberModel>> Handle(GetTeamMembersQuery request, CancellationToken cancellationToken)
    {
        var members = await _memberRepository.GetByTeam(PrimitiveParser.ParseId(request.TeamId), cancellationToken);
        return members
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ExternalUserId, StringComparer.OrdinalIgnoreCase)
            .Select(x => new TeamMemberModel
            {
                Id = x.Id.ToString(),
                TeamId = x.TeamId.ToString(),
                ExternalUserId = x.ExternalUserId,
                DisplayName = x.DisplayName ?? string.Empty,
                CreatedOnUtc = x.CreatedOnUtc.ToString("O"),
            })
            .ToArray();
    }
}
