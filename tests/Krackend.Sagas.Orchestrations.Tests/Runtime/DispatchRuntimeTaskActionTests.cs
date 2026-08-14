using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;
using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.Engine.DurableWork;
using Microsoft.Extensions.DependencyInjection;
using Mule;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class DispatchRuntimeTaskActionTests
{
    [Fact]
    public async Task ExecuteAsync_Should_Publish_Message_Dispatch()
    {
        var dispatcher = new CapturingMessagingCommandDispatcher();
        var action = new DispatchRuntimeTaskAction(dispatcher);

        await action.ExecuteAsync(CreateContext(CreateEnvelope()), CancellationToken.None);

        Assert.Equal("orders.payment", dispatcher.Command.Destination);
        Assert.Equal("1.0.0", dispatcher.Command.MessageVersion);
        Assert.Equal("local", dispatcher.Command.EnvironmentKey);
        Assert.Equal("dispatch-1", dispatcher.Command.DispatchId);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Reject_Unsupported_Transport()
    {
        var dispatcher = new CapturingMessagingCommandDispatcher();
        var action = new DispatchRuntimeTaskAction(dispatcher);
        var envelope = CreateEnvelope();
        envelope.Destination.Kind = RuntimeTransportKind.Http;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => action.ExecuteAsync(CreateContext(envelope), CancellationToken.None).AsTask());

        Assert.Equal("Runtime dispatch transport 'Http' is not supported yet.", ex.Message);
    }

    private static RuntimeDispatchEnvelope CreateEnvelope()
        => new()
        {
            DispatchId = "dispatch-1",
            OrchestrationInstanceId = "instance-1",
            ExecutionKey = "cmd-1",
            CorrelationId = "corr-1",
            SagaId = "saga-1",
            EnvironmentKey = "local",
            OrchestrationName = "order.fulfillment",
            OrchestrationVersion = "2.0.0",
            StageKey = "payment",
            TaskKey = "charge-payment",
            TaskExecutionId = "task-exec-1",
            Attempt = 1,
            Payload = JsonNode.Parse("""{"amount":10}""")!,
            CreatedOnUtc = new DateTime(2026, 8, 14, 1, 2, 3, DateTimeKind.Utc),
            Destination = new RuntimeTransportDescriptor
            {
                Kind = RuntimeTransportKind.Message,
                Address = "orders.payment",
                Version = "1.0.0"
            }
        };

    private static MuleActionContext<RuntimeDispatchEnvelope> CreateContext(RuntimeDispatchEnvelope envelope)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var action = new DurableAction
        {
            Id = Guid.NewGuid(),
            Key = RuntimeDurableWorkActionKeys.DispatchTask,
            Payload = "{}",
            PayloadType = typeof(RuntimeDispatchEnvelope).AssemblyQualifiedName,
            Status = DurableActionStatus.Locked,
            CreatedOnUtc = DateTimeOffset.UtcNow
        };

        return new MuleActionContext<RuntimeDispatchEnvelope>(action, services, envelope);
    }

    private sealed class CapturingMessagingCommandDispatcher : IMessagingCommandDispatcher
    {
        public MessagingDispatchCommand Command { get; private set; } = null!;

        public Task<MessagingDispatchResult> Dispatch(MessagingDispatchCommand command, CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(new MessagingDispatchResult
            {
                Succeeded = true,
                Status = "Published",
                ExternalReference = "message-1"
            });
        }
    }
}
