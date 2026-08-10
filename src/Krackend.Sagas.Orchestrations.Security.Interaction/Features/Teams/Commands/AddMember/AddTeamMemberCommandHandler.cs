using Krackend.Sagas.Orchestrations.Security.Core;
using Krackend.Sagas.Orchestrations.Security.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Security.Interaction;

/// <summary>
/// Handles add team member command requests.
/// </summary>
public sealed class AddTeamMemberCommandHandler : IRequestHandler<AddTeamMemberCommand, bool>
{
    private readonly ITeamRepository _teamRepository;
    private readonly ITeamMemberRepository _memberRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddTeamMemberCommandHandler"/> class.
    /// </summary>
    /// <param name="teamRepository">Team repository dependency.</param>
    /// <param name="memberRepository">Team member repository dependency.</param>
    public AddTeamMemberCommandHandler(ITeamRepository teamRepository, ITeamMemberRepository memberRepository)
    {
        _teamRepository = teamRepository ?? throw new ArgumentNullException(nameof(teamRepository));
        _memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
    }

    /// <inheritdoc />
    public async Task<bool> Handle(AddTeamMemberCommand request, CancellationToken cancellationToken)
    {
        var teamId = PrimitiveParser.ParseId(request.TeamId);
        var team = await _teamRepository.GetById(teamId, cancellationToken);
        if (!team.IsActive)
        {
            throw new InvalidOperationException("Cannot add members to a disabled team.");
        }

        var externalUserId = request.ExternalUserId.Trim();
        if (await _memberRepository.Exists(teamId, externalUserId, cancellationToken))
        {
            return true;
        }

        await _memberRepository.Add(new TeamMember
        {
            Id = new Krackend.Sagas.Orchestrations.Abstractions.Primitives.Id(Ulid.NewUlid()),
            TeamId = teamId,
            ExternalUserId = externalUserId,
            DisplayName = request.DisplayName?.Trim() ?? string.Empty,
            CreatedOnUtc = DateTime.UtcNow,
        }, cancellationToken);

        return true;
    }
}
