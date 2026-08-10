namespace Krackend.Sagas.Orchestrations.Contracts.Eventing;

public interface IIntegrationEventPublisher
{
    Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent;
}
