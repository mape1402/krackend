using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;
using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.Engine.DurableWork;
using Microsoft.Extensions.DependencyInjection;
using Mule;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class ProcessRuntimeIngressActionTests
{
    [Fact]
    public async Task ExecuteAsync_Should_Enqueue_Trigger_And_Process_Next()
    {
        var intake = new CapturingTriggerIntakeBuffer();
        var engine = new CapturingRuntimeEngine();
        var action = new ProcessRuntimeIngressAction(intake, engine);
        var envelope = new RuntimeIngressEnvelope
        {
            Kind = RuntimeIngressKind.Trigger,
            EnvironmentKey = "local",
            OrchestrationName = "order.fulfillment",
            OrchestrationVersion = "1.0.0",
            CorrelationId = "corr-1",
            Payload = JsonNode.Parse("""{"orderId":"A1"}"""),
            Source = new RuntimeTransportDescriptor
            {
                Kind = RuntimeTransportKind.Message,
                MessageId = "msg-1"
            }
        };

        await action.ExecuteAsync(CreateContext(envelope), CancellationToken.None);

        Assert.Equal("order.fulfillment", intake.EnqueuedItem.TriggerKey);
        Assert.Equal("trigger|local|order.fulfillment|1.0.0|msg-1|corr-1", intake.EnqueuedItem.IdempotencyKey);
        Assert.Equal(1, engine.ProcessNextCalls);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Continue_Task_Response()
    {
        var intake = new CapturingTriggerIntakeBuffer();
        var engine = new CapturingRuntimeEngine();
        var action = new ProcessRuntimeIngressAction(intake, engine);
        var envelope = new RuntimeIngressEnvelope
        {
            Kind = RuntimeIngressKind.TaskResponse,
            EnvironmentKey = "local",
            OrchestrationName = "order.fulfillment",
            OrchestrationVersion = "1.0.0",
            CorrelationId = "corr-1",
            OrchestrationInstanceId = "instance-1",
            DispatchId = "dispatch-1",
            TaskExecutionId = "task-exec-1",
            Payload = JsonNode.Parse("""{"status":"paid"}""")
        };

        await action.ExecuteAsync(CreateContext(envelope), CancellationToken.None);

        Assert.Equal("instance-1", engine.ResponseCommand.OrchestrationInstanceId);
        Assert.Equal("dispatch-1", engine.ResponseCommand.DispatchId);
        Assert.Equal("task-exec-1", engine.ResponseCommand.TaskExecutionId);
        Assert.Equal(0, engine.ProcessNextCalls);
    }

    private static MuleActionContext<RuntimeIngressEnvelope> CreateContext(RuntimeIngressEnvelope envelope)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var action = new DurableAction
        {
            Id = Guid.NewGuid(),
            Key = RuntimeDurableWorkActionKeys.ProcessIngress,
            Payload = "{}",
            PayloadType = typeof(RuntimeIngressEnvelope).AssemblyQualifiedName,
            Status = DurableActionStatus.Locked,
            CreatedOnUtc = DateTimeOffset.UtcNow
        };

        return new MuleActionContext<RuntimeIngressEnvelope>(action, services, envelope);
    }

    private sealed class CapturingTriggerIntakeBuffer : ITriggerIntakeBuffer
    {
        public TriggerIntakeBufferItem EnqueuedItem { get; private set; } = null!;

        public Task<TriggerIntakeBufferResult> Enqueue(TriggerIntakeBufferItem item, CancellationToken cancellationToken = default)
        {
            EnqueuedItem = item;
            return Task.FromResult(TriggerIntakeBufferResult.Accept(item.BufferItemId));
        }

        public Task<TriggerIntakeBufferLease> TryDequeue(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<TriggerIntakeBufferItem> Peek(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<TriggerIntakeBufferResult> MarkCompleted(Id bufferItemId, string leaseId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<TriggerIntakeBufferResult> MarkFailed(Id bufferItemId, string leaseId, string reason, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class CapturingRuntimeEngine : IRuntimeEngine
    {
        public int ProcessNextCalls { get; private set; }

        public RuntimeMessageResponseCommand ResponseCommand { get; private set; } = null!;

        public Task<RuntimeEngineProcessResult> ProcessNext(CancellationToken cancellationToken = default)
        {
            ProcessNextCalls++;
            return Task.FromResult(CreateResult());
        }

        public Task<IReadOnlyCollection<RuntimeEngineProcessResult>> ProcessAll(int maxItems = 25, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RuntimeEngineProcessResult> ContinueFromResponse(RuntimeMessageResponseCommand command, CancellationToken cancellationToken = default)
        {
            ResponseCommand = command;
            return Task.FromResult(CreateResult());
        }

        private static RuntimeEngineProcessResult CreateResult()
            => new()
            {
                Succeeded = true,
                Status = "Processed",
                Message = "Processed"
            };
    }
}
