namespace Krackend.Sagas.Orchestrations.Contracts.Eventing;

public interface IIntegrationEventHandler<in TEvent>
    where TEvent : class, IIntegrationEvent
{
    Task Handle(TEvent @event, CancellationToken cancellationToken = default);
}
