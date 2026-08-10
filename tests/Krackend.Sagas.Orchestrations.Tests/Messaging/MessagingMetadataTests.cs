using Krackend.Sagas.Orchestrations.Messaging.Abstractions;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.Messaging;

public sealed class MessagingMetadataTests
{
    [Fact]
    public void MetadataContextSharesEnvelopeBetweenWriterAndAccessor()
    {
        var services = new ServiceCollection();

        services.AddKrackendSagasOrchestrationsMessaging();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var accessor = scope.ServiceProvider.GetRequiredService<IOrchestratorMetadataAccessor>();
        var writer = scope.ServiceProvider.GetRequiredService<IOrchestratorMetadataWriter>();
        var metadata = CreateMetadata();

        writer.Set(metadata);

        Assert.Equal("Orchestrator.Metadata", OrchestratorMetadataConstants.MetadataKey);
        Assert.Same(metadata, accessor.Current);

        writer.Clear();

        Assert.Null(accessor.Current);
    }

    private static OrchestratorMessageMetadata CreateMetadata()
    {
        var now = DateTime.UtcNow;

        return new OrchestratorMessageMetadata
        {
            SagaId = "saga-1",
            OrchestrationId = "orch-1",
            OrchestrationKey = "order-fulfillment",
            OrchestrationVersion = "1.0.0",
            OrchestrationInstanceId = "instance-1",
            TaskExecutionId = "task-1",
            DispatchId = "dispatch-1",
            CorrelationId = "corr-1",
            CurrentState = new OrchestrationRuntimeState
            {
                Status = "Running",
                CurrentStageKey = "payment",
                CurrentTaskKey = "authorize",
                Attempt = 1,
                StartedOnUtc = now,
                UpdatedOnUtc = now
            },
            ResponseTopic = "orders.responses",
            ResponseVersion = "1.0.0",
            Environment = "local"
        };
    }
}
