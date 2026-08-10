using Krackend.Sagas.Orchestrations.Contracts.Eventing;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.Distribution.Core;
using Krackend.Sagas.Orchestrations.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public sealed class OrchestrationDefinitionCreatedEventHandler : IIntegrationEventHandler<OrchestrationDefinitionCreatedEvent>
{
    private readonly IOrchestrationProjectionRepository _repository;

    public OrchestrationDefinitionCreatedEventHandler(IOrchestrationProjectionRepository repository)
    {
        _repository = repository;
    }

    public Task Handle(OrchestrationDefinitionCreatedEvent integrationEvent, CancellationToken cancellationToken = default)
        => _repository.Upsert(new OrchestrationProjection
        {
            Id = integrationEvent.OrchestrationDefinitionId,
            Key = integrationEvent.Key,
            Name = integrationEvent.Name,
            IsActive = integrationEvent.IsActive,
            CreatedAtUtc = integrationEvent.OccurredAtUtc,
            UpdatedAtUtc = integrationEvent.OccurredAtUtc
        }, cancellationToken);
}

public sealed class OrchestrationDefinitionUpdatedEventHandler : IIntegrationEventHandler<OrchestrationDefinitionUpdatedEvent>
{
    private readonly IOrchestrationProjectionRepository _repository;

    public OrchestrationDefinitionUpdatedEventHandler(IOrchestrationProjectionRepository repository)
    {
        _repository = repository;
    }

    public Task Handle(OrchestrationDefinitionUpdatedEvent integrationEvent, CancellationToken cancellationToken = default)
        => _repository.Upsert(new OrchestrationProjection
        {
            Id = integrationEvent.OrchestrationDefinitionId,
            Key = integrationEvent.Key,
            Name = integrationEvent.Name,
            IsActive = integrationEvent.IsActive,
            CreatedAtUtc = integrationEvent.OccurredAtUtc,
            UpdatedAtUtc = integrationEvent.OccurredAtUtc
        }, cancellationToken);
}

public sealed class OrchestrationDefinitionDeactivatedEventHandler : IIntegrationEventHandler<OrchestrationDefinitionDeactivatedEvent>
{
    private readonly IOrchestrationProjectionRepository _repository;

    public OrchestrationDefinitionDeactivatedEventHandler(IOrchestrationProjectionRepository repository)
    {
        _repository = repository;
    }

    public Task Handle(OrchestrationDefinitionDeactivatedEvent integrationEvent, CancellationToken cancellationToken = default)
        => _repository.Upsert(new OrchestrationProjection
        {
            Id = integrationEvent.OrchestrationDefinitionId,
            Key = integrationEvent.Key,
            Name = integrationEvent.Name,
            IsActive = integrationEvent.IsActive,
            CreatedAtUtc = integrationEvent.OccurredAtUtc,
            UpdatedAtUtc = integrationEvent.OccurredAtUtc
        }, cancellationToken);
}


