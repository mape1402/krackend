using Krackend.Sagas.Orchestrations.Contracts.Eventing;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Storage;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles team created events and updates local Design projection.
/// </summary>
public sealed class TeamCreatedEventHandler : IIntegrationEventHandler<TeamCreatedEvent>
{
    private readonly ITeamProjectionRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamCreatedEventHandler"/> class.
    /// </summary>
    /// <param name="repository">Team projection repository dependency.</param>
    public TeamCreatedEventHandler(ITeamProjectionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public Task Handle(TeamCreatedEvent integrationEvent, CancellationToken cancellationToken = default)
        => _repository.Upsert(new TeamProjection
        {
            Id = PrimitiveParser.ParseId(integrationEvent.TeamId),
            Key = integrationEvent.Key,
            DisplayName = integrationEvent.DisplayName,
            IsActive = integrationEvent.IsActive,
            UpdatedAtUtc = integrationEvent.OccurredAtUtc,
        }, cancellationToken);
}

/// <summary>
/// Handles team updated events and updates local Design projection.
/// </summary>
public sealed class TeamUpdatedEventHandler : IIntegrationEventHandler<TeamUpdatedEvent>
{
    private readonly ITeamProjectionRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamUpdatedEventHandler"/> class.
    /// </summary>
    /// <param name="repository">Team projection repository dependency.</param>
    public TeamUpdatedEventHandler(ITeamProjectionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public Task Handle(TeamUpdatedEvent integrationEvent, CancellationToken cancellationToken = default)
        => _repository.Upsert(new TeamProjection
        {
            Id = PrimitiveParser.ParseId(integrationEvent.TeamId),
            Key = integrationEvent.Key,
            DisplayName = integrationEvent.DisplayName,
            IsActive = integrationEvent.IsActive,
            UpdatedAtUtc = integrationEvent.OccurredAtUtc,
        }, cancellationToken);
}

/// <summary>
/// Handles team disabled events and updates local Design projection.
/// </summary>
public sealed class TeamDisabledEventHandler : IIntegrationEventHandler<TeamDisabledEvent>
{
    private readonly ITeamProjectionRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamDisabledEventHandler"/> class.
    /// </summary>
    /// <param name="repository">Team projection repository dependency.</param>
    public TeamDisabledEventHandler(ITeamProjectionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public Task Handle(TeamDisabledEvent integrationEvent, CancellationToken cancellationToken = default)
        => _repository.Upsert(new TeamProjection
        {
            Id = PrimitiveParser.ParseId(integrationEvent.TeamId),
            Key = integrationEvent.Key,
            DisplayName = integrationEvent.DisplayName,
            IsActive = false,
            UpdatedAtUtc = integrationEvent.OccurredAtUtc,
        }, cancellationToken);
}
