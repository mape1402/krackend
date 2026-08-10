using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Krackend.Sagas.Orchestrations.Contracts.Eventing;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Bootstrap.Eventing;

internal sealed class InProcessIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InProcessIntegrationEventPublisher> _logger;

    public InProcessIntegrationEventPublisher(
        IServiceProvider serviceProvider,
        ILogger<InProcessIntegrationEventPublisher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task Publish<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        var handlers = _serviceProvider.GetServices<IIntegrationEventHandler<TEvent>>().ToArray();
        if (handlers.Length == 0)
        {
            _logger.LogWarning(
                "No integration event handlers registered for {EventType}. CorrelationId={CorrelationId}",
                typeof(TEvent).Name,
                integrationEvent.CorrelationId);
            return;
        }

        foreach (var handler in handlers)
        {
            await handler.Handle(integrationEvent, cancellationToken);
        }
    }
}
