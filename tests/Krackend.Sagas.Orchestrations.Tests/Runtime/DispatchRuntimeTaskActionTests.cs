using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
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
        var dispatchRepository = new CapturingTaskDispatchRepository();
        var action = new DispatchRuntimeTaskAction(dispatcher, dispatchRepository);
        var envelope = CreateEnvelope();
        dispatchRepository.Dispatch.Id = new Id(Ulid.Parse(envelope.DispatchId));

        await action.ExecuteAsync(CreateContext(envelope), CancellationToken.None);

        Assert.Equal("orders.payment", dispatcher.Command.Destination);
        Assert.Equal("1.0.0", dispatcher.Command.MessageVersion);
        Assert.Equal("local", dispatcher.Command.EnvironmentKey);
        Assert.Equal(envelope.DispatchId, dispatcher.Command.DispatchId);
        Assert.Equal("Published", dispatchRepository.Dispatch.DispatchStatus);
        Assert.NotNull(dispatchRepository.Dispatch.SentOnUtc);
        Assert.Equal("message-1", dispatchRepository.Dispatch.Metadata["externalReference"]!.GetValue<string>());
    }

    [Fact]
    public async Task ExecuteAsync_Should_Reject_Unsupported_Transport()
    {
        var dispatcher = new CapturingMessagingCommandDispatcher();
        var action = new DispatchRuntimeTaskAction(dispatcher, new CapturingTaskDispatchRepository());
        var envelope = CreateEnvelope();
        envelope.Destination.Kind = RuntimeTransportKind.Http;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => action.ExecuteAsync(CreateContext(envelope), CancellationToken.None).AsTask());

        Assert.Equal("Runtime dispatch transport 'Http' is not supported yet.", ex.Message);
    }

    private static RuntimeDispatchEnvelope CreateEnvelope()
        => new()
        {
            DispatchId = Id.New().ToString(),
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

    private sealed class CapturingTaskDispatchRepository : ITaskDispatchRepository
    {
        public Abstractions.Runtime.TaskDispatch Dispatch { get; } = new()
        {
            Id = Id.New(),
            TaskExecutionAttemptId = Id.New(),
            DispatchType = "Messaging",
            Destination = "orders.payment",
            DispatchStatus = "Scheduled",
            Metadata = new Dictionary<string, JsonNode>()
        };

        public Task Create(Abstractions.Runtime.TaskDispatch dispatch, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task Update(Abstractions.Runtime.TaskDispatch dispatch, CancellationToken cancellationToken = default)
        {
            Dispatch.DispatchStatus = dispatch.DispatchStatus;
            Dispatch.ScheduledOnUtc = dispatch.ScheduledOnUtc;
            Dispatch.SentOnUtc = dispatch.SentOnUtc;
            Dispatch.AcknowledgedOnUtc = dispatch.AcknowledgedOnUtc;
            Dispatch.FailedOnUtc = dispatch.FailedOnUtc;
            Dispatch.FailureReason = dispatch.FailureReason;
            Dispatch.Metadata = dispatch.Metadata;
            return Task.CompletedTask;
        }

        public Task<Abstractions.Runtime.TaskDispatch> GetById(Id dispatchId, CancellationToken cancellationToken = default)
            => Task.FromResult(Dispatch);

        public Task<Abstractions.Runtime.TaskDispatch> TryGetById(Id dispatchId, CancellationToken cancellationToken = default)
            => Task.FromResult(dispatchId == Dispatch.Id ? Dispatch : null!);

        public Task<Abstractions.Runtime.TaskDispatch> GetByCommandId(string commandId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Abstractions.Runtime.TaskDispatch> GetByAttemptId(Id taskExecutionAttemptId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<Abstractions.Runtime.TaskDispatch>> GetScheduledOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<Abstractions.Runtime.TaskDispatch>>(Array.Empty<Abstractions.Runtime.TaskDispatch>());
    }
}
