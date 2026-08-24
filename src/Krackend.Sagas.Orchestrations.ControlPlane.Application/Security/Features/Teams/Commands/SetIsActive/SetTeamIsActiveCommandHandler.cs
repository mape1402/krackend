using Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

/// <summary>
/// Handles team active state changes.
/// </summary>
public sealed class SetTeamIsActiveCommandHandler : IRequestHandler<SetTeamIsActiveCommand, bool>
{
    private readonly ITeamRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetTeamIsActiveCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Team repository dependency.</param>
    public SetTeamIsActiveCommandHandler(ITeamRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public async Task<bool> Handle(SetTeamIsActiveCommand request, CancellationToken cancellationToken)
    {
        var teamId = PrimitiveParser.ParseId(request.TeamId);
        await _repository.SetIsActive(teamId, request.IsActive, cancellationToken);
        return true;
    }
}
