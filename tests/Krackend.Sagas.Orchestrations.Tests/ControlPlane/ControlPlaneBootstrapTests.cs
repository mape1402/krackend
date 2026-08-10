using Krackend.Sagas.Orchestrations.Contracts.Eventing;
using Krackend.Sagas.Orchestrations.ControlPlane.Bootstrap;
using Krackend.Sagas.Orchestrations.Design.Interaction;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Krackend.Sagas.Orchestrations.Distribution.Interaction;
using Krackend.Sagas.Orchestrations.Distribution.Storage;
using Krackend.Sagas.Orchestrations.Security.Interaction;
using Krackend.Sagas.Orchestrations.Security.Storage;
using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Krackend.Sagas.Orchestrations.Tests.ControlPlane;

public sealed class ControlPlaneBootstrapTests
{
    [Fact]
    public void AddOrchestratorControlPlaneRequiresSqlServerConfiguration()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddOrchestratorControlPlane(_ => { }));
    }

    [Fact]
    public void AddOrchestratorControlPlaneRegistersCrossModuleServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddOrchestratorControlPlane(options =>
        {
            options.AdminRootPath = "/admin/";
            options.ConfigureSqlServer = _ => { };
        });

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(OrchestratorNavigationRegistry));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IIntegrationEventPublisher));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IOrchestrationInteractionService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IDomainRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRuntimeNodeInteractionService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IEnvironmentRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITeamInteractionService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITeamRepository));
    }

    [Fact]
    public async Task InProcessPublisherDispatchesEventToAllRegisteredHandlers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOrchestratorControlPlane(options =>
        {
            options.ConfigureSqlServer = _ => { };
        });
        services.AddSingleton<RecordingIntegrationEventHandler>();
        services.AddSingleton<IIntegrationEventHandler<TestIntegrationEvent>>(provider =>
            provider.GetRequiredService<RecordingIntegrationEventHandler>());

        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IIntegrationEventPublisher>();
        var handler = provider.GetRequiredService<RecordingIntegrationEventHandler>();
        var integrationEvent = new TestIntegrationEvent("correlation-1", DateTime.UtcNow);

        await publisher.Publish(integrationEvent);

        Assert.Same(integrationEvent, handler.Events.Single());
    }

    private sealed record TestIntegrationEvent(string CorrelationId, DateTime OccurredAtUtc) : IIntegrationEvent;

    private sealed class RecordingIntegrationEventHandler : IIntegrationEventHandler<TestIntegrationEvent>
    {
        private readonly List<TestIntegrationEvent> _events = [];

        public IReadOnlyCollection<TestIntegrationEvent> Events => _events;

        public Task Handle(TestIntegrationEvent @event, CancellationToken cancellationToken = default)
        {
            _events.Add(@event);
            return Task.CompletedTask;
        }
    }
}
