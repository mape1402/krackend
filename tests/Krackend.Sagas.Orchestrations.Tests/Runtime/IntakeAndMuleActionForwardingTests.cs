namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Runtime.Buffering;
using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Microsoft.Extensions.DependencyInjection;
using Mule;
using NSubstitute;

public sealed class IntakeAndMuleActionForwardingTests
{
    [Fact]
    public async Task TriggerActionStartsOrchestrationWithWorkItemData()
    {
        var engine = Substitute.For<ISagaEngine>();
        var action = new TriggerAction(engine);
        var workItem = WorkItem();
        var context = Context("TriggerSaga", workItem);

        await action.ExecuteAsync(context, CancellationToken.None);

        await engine.Received(1).StartOrchestrationAsync(
            Arg.Is<StartIntent>(intent =>
                intent.ArtifactId == workItem.ArtifactId &&
                intent.IngressTransport == workItem.IngressTransport &&
                ReferenceEquals(intent.MessageMetadata, workItem.MessageMetadata) &&
                intent.Payload!["saleId"]!.GetValue<string>() == "sale-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackchannelActionForwardsExecutionResultMetadataWithPayload()
    {
        var engine = Substitute.For<ISagaEngine>();
        var action = new BackchannelAction(engine);
        var workItem = WorkItem();
        var context = Context("BackchannelSaga", workItem);

        await action.ExecuteAsync(context, CancellationToken.None);

        await engine.Received(1).OrchestrateAsync(
            Arg.Is<ForwardIntent>(intent =>
                intent.ArtifactId == workItem.ArtifactId &&
                intent.IngressTransport == workItem.IngressTransport &&
                ReferenceEquals(intent.MessageMetadata, workItem.MessageMetadata) &&
                ReferenceEquals(intent.ExecutionResultMetadata, workItem.ExecutionResultMetadata) &&
                intent.Payload!["saleId"]!.GetValue<string>() == "sale-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DefaultRuntimeIntakeBufferIgnoresWorkWithoutThrowing()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var buffer = scope.ServiceProvider.GetRequiredService<IIntakeBuffer>();

        await buffer.EnqueueWorkAsync(WorkItem(), CancellationToken.None);
    }

    private static WorkItem WorkItem()
        => new()
        {
            ArtifactId = Id.New().ToString(),
            IngressTransport = IngressTransport.Messaging,
            MessageMetadata = new OrchestrationMessageMetadata
            {
                OrchestrationInstanceId = Id.New().ToString(),
                CorrelationId = "correlation-1",
                SagaId = "saga-1",
                CurrentStage = "inventory",
                CurrentTasks = ["inventories.reserve"],
                ReplyAddress = new OrchestrationReplyAddress
                {
                    Transport = "messaging",
                    SettingsPayload = """{"topic":"orchestrations.sales.sale.created","version":"1.0.0"}"""
                }
            },
            ExecutionResultMetadata = new OrchestrationExecutionResultMetadata
            {
                Succeeded = true,
                Status = "Succeeded",
                ServiceName = "Inventories",
                OperationName = "inventories.reserve"
            },
            Payload = JsonNode.Parse("""{"saleId":"sale-1"}""")
        };

    private static MuleActionContext<WorkItem> Context(string actionKey, WorkItem workItem)
        => new(
            new DurableAction
            {
                Key = ActionKey.From(actionKey),
                Lane = "default",
                CorrelationId = "correlation-1",
                DeduplicationKey = Id.New().ToString(),
                Status = DurableActionStatus.Pending
            },
            new ServiceCollection().BuildServiceProvider(),
            workItem);
}
