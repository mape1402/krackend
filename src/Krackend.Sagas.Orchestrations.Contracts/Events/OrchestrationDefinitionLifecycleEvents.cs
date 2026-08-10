using Krackend.Sagas.Orchestrations.Contracts.Eventing;

namespace Krackend.Sagas.Orchestrations.Contracts.Events;

public sealed record OrchestrationDefinitionCreatedEvent(
    string OrchestrationDefinitionId,
    string Key,
    string Name,
    bool IsActive,
    string Actor,
    string CorrelationId,
    DateTime OccurredAtUtc) : IIntegrationEvent;

public sealed record OrchestrationDefinitionUpdatedEvent(
    string OrchestrationDefinitionId,
    string Key,
    string Name,
    bool IsActive,
    string Actor,
    string CorrelationId,
    DateTime OccurredAtUtc) : IIntegrationEvent;

public sealed record OrchestrationDefinitionDeactivatedEvent(
    string OrchestrationDefinitionId,
    string Key,
    string Name,
    bool IsActive,
    string Actor,
    string CorrelationId,
    DateTime OccurredAtUtc) : IIntegrationEvent;

