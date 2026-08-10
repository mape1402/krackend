using Krackend.Sagas.Orchestrations.Contracts.Eventing;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.Security.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Security.Interaction;

/// <summary>
/// Handles team active state changes.
/// </summary>
public sealed class SetTeamIsActiveCommandHandler : IRequestHandler<SetTeamIsActiveCommand, bool>
{
    private readonly ITeamRepository _repository;
    private readonly IIntegrationEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetTeamIsActiveCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Team repository dependency.</param>
    /// <param name="eventPublisher">Event publisher dependency.</param>
    public SetTeamIsActiveCommandHandler(ITeamRepository repository, IIntegrationEventPublisher eventPublisher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    /// <inheritdoc />
    public async Task<bool> Handle(SetTeamIsActiveCommand request, CancellationToken cancellationToken)
    {
        var teamId = PrimitiveParser.ParseId(request.TeamId);
        await _repository.SetIsActive(teamId, request.IsActive, cancellationToken);
        var team = await _repository.GetById(teamId, cancellationToken);

        if (request.IsActive)
        {
            await _eventPublisher.Publish(new TeamUpdatedEvent(
                team.Id.ToString(),
                team.Key,
                team.DisplayName,
                team.IsActive,
                request.Actor,
                team.Id.ToString(),
                DateTime.UtcNow), cancellationToken);
        }
        else
        {
            await _eventPublisher.Publish(new TeamDisabledEvent(
                team.Id.ToString(),
                team.Key,
                team.DisplayName,
                false,
                request.Actor,
                team.Id.ToString(),
                DateTime.UtcNow), cancellationToken);
        }

        return true;
    }
}
