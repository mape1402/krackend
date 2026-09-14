using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

/// <summary>
/// Provides interaction operations for teams.
/// </summary>
public sealed class TeamApplicationService : ITeamApplicationService
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamApplicationService"/> class.
    /// </summary>
    /// <param name="mediator">Mediator dependency.</param>
    public TeamApplicationService(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <inheritdoc />
    public Task<string> Upsert(UpsertTeamCommand command, CancellationToken cancellationToken = default)
        => _mediator.Send(command, cancellationToken);

    /// <inheritdoc />
    public Task<bool> SetIsActive(SetTeamIsActiveCommand command, CancellationToken cancellationToken = default)
        => _mediator.Send(command, cancellationToken);

    /// <inheritdoc />
    public Task<bool> AddMember(AddTeamMemberCommand command, CancellationToken cancellationToken = default)
        => _mediator.Send(command, cancellationToken);

    /// <inheritdoc />
    public Task<bool> RemoveMember(RemoveTeamMemberCommand command, CancellationToken cancellationToken = default)
        => _mediator.Send(command, cancellationToken);

    /// <inheritdoc />
    public Task<ApplicationPagedResult<TeamModel>> GetAll(GetTeamsQuery query, CancellationToken cancellationToken = default)
        => _mediator.Send(query, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyCollection<TeamMemberModel>> GetMembers(GetTeamMembersQuery query, CancellationToken cancellationToken = default)
        => _mediator.Send(query, cancellationToken);
}
