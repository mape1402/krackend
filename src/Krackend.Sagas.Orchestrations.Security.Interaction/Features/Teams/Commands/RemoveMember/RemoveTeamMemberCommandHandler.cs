using Krackend.Sagas.Orchestrations.Security.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Security.Interaction;

/// <summary>
/// Handles remove team member command requests.
/// </summary>
public sealed class RemoveTeamMemberCommandHandler : IRequestHandler<RemoveTeamMemberCommand, bool>
{
    private readonly ITeamMemberRepository _memberRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveTeamMemberCommandHandler"/> class.
    /// </summary>
    /// <param name="memberRepository">Team member repository dependency.</param>
    public RemoveTeamMemberCommandHandler(ITeamMemberRepository memberRepository)
    {
        _memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
    }

    /// <inheritdoc />
    public async Task<bool> Handle(RemoveTeamMemberCommand request, CancellationToken cancellationToken)
    {
        await _memberRepository.Remove(PrimitiveParser.ParseId(request.TeamId), request.ExternalUserId.Trim(), cancellationToken);
        return true;
    }
}
