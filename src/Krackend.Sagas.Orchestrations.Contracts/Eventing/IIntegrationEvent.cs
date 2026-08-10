namespace Krackend.Sagas.Orchestrations.Contracts.Eventing;

public interface IIntegrationEvent
{
    string CorrelationId { get; }
    DateTime OccurredAtUtc { get; }
}
