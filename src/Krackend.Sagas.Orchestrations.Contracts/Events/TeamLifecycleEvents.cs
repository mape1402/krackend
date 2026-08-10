using Krackend.Sagas.Orchestrations.Contracts.Eventing;

namespace Krackend.Sagas.Orchestrations.Contracts.Events;

public sealed record TeamCreatedEvent(
    string TeamId,
    string Key,
    string DisplayName,
    bool IsActive,
    string Actor,
    string CorrelationId,
    DateTime OccurredAtUtc) : IIntegrationEvent;

public sealed record TeamUpdatedEvent(
    string TeamId,
    string Key,
    string DisplayName,
    bool IsActive,
    string Actor,
    string CorrelationId,
    DateTime OccurredAtUtc) : IIntegrationEvent;

public sealed record TeamDisabledEvent(
    string TeamId,
    string Key,
    string DisplayName,
    bool IsActive,
    string Actor,
    string CorrelationId,
    DateTime OccurredAtUtc) : IIntegrationEvent;
