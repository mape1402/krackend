using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;
using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.Engine.DurableWork;
using Microsoft.Extensions.DependencyInjection;
using Mule;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class MuleRuntimeDurableWorkSchedulerTests
{
    [Fact]
    public async Task ScheduleProcessIngress_Should_Use_ProcessIngress_Action_With_Metadata()
    {
        var mule = new CapturingMuleClient();
        var scheduler = new MuleRuntimeDurableWorkScheduler(mule);
        var envelope = new RuntimeIngressEnvelope
        {
            Kind = RuntimeIngressKind.TaskResponse,
            EnvironmentKey = "local",
            OrchestrationName = "order.fulfillment",
            OrchestrationVersion = "1.0.0",
            CorrelationId = "corr-1",
            SagaId = "saga-1",
            OrchestrationInstanceId = "instance-1",
            DispatchId = "dispatch-1",
            TaskExecutionId = "task-exec-1",
            Source = new RuntimeTransportDescriptor
            {
                Kind = RuntimeTransportKind.Message,
                Address = "orders.backchannel",
                MessageId = "msg-1"
            },
            Payload = JsonNode.Parse("""{"ok":true}""")
        };

        await scheduler.ScheduleProcessIngress(envelope);

        Assert.Equal(RuntimeDurableWorkActionKeys.ProcessIngress, mule.CapturedKey);
        Assert.Same(envelope, mule.CapturedPayload);
        Assert.Equal(RuntimeDurableWorkLanes.ResponseIngress, mule.CapturedLane);
        Assert.Equal("corr-1", mule.CapturedCorrelationId);
        Assert.Equal("response|local|instance-1|dispatch-1|task-exec-1|0|msg-1", mule.CapturedDeduplicationKey);
        Assert.Equal("TaskResponse", mule.CapturedMetadata["runtime.ingress.kind"]);
        Assert.Equal("orders.backchannel", mule.CapturedMetadata["runtime.transport.address"]);
    }

    [Fact]
    public async Task ScheduleProcessIngress_Should_Use_TriggerIngress_Lane_For_Triggers()
    {
        var mule = new CapturingMuleClient();
        var scheduler = new MuleRuntimeDurableWorkScheduler(mule);
        var envelope = new RuntimeIngressEnvelope
        {
            Kind = RuntimeIngressKind.Trigger,
            EnvironmentKey = "local",
            OrchestrationName = "order.fulfillment",
            OrchestrationVersion = "1.0.0",
            CorrelationId = "corr-1",
            Source = new RuntimeTransportDescriptor
            {
                Kind = RuntimeTransportKind.Message,
                Address = "orders.created",
                MessageId = "msg-1"
            },
            Payload = JsonNode.Parse("""{"ok":true}""")
        };

        await scheduler.ScheduleProcessIngress(envelope);

        Assert.Equal(RuntimeDurableWorkLanes.TriggerIngress, mule.CapturedLane);
    }

    [Fact]
    public async Task ScheduleDispatchTask_Should_Use_DispatchTask_Action_With_Metadata()
    {
        var mule = new CapturingMuleClient();
        var scheduler = new MuleRuntimeDurableWorkScheduler(mule);
        var envelope = new RuntimeDispatchEnvelope
        {
            DispatchId = "dispatch-1",
            OrchestrationInstanceId = "instance-1",
            CorrelationId = "corr-1",
            SagaId = "saga-1",
            EnvironmentKey = "local",
            OrchestrationName = "order.fulfillment",
            OrchestrationVersion = "1.0.0",
            StageKey = "payment",
            TaskKey = "charge-payment",
            TaskExecutionId = "task-exec-1",
            Attempt = 3,
            Payload = JsonNode.Parse("""{"amount":10}""")!,
            Destination = new RuntimeTransportDescriptor
            {
                Kind = RuntimeTransportKind.Message,
                Address = "orders.payment",
                Version = "2.0.0"
            }
        };

        await scheduler.ScheduleDispatchTask(envelope);

        Assert.Equal(RuntimeDurableWorkActionKeys.DispatchTask, mule.CapturedKey);
        Assert.Same(envelope, mule.CapturedPayload);
        Assert.Equal(RuntimeDurableWorkLanes.Dispatch, mule.CapturedLane);
        Assert.Equal("corr-1", mule.CapturedCorrelationId);
        Assert.Equal("dispatch|instance-1|dispatch-1|task-exec-1|3", mule.CapturedDeduplicationKey);
        Assert.Equal("charge-payment", mule.CapturedMetadata["runtime.task"]);
        Assert.Equal("2.0.0", mule.CapturedMetadata["runtime.transport.version"]);
    }

    [Fact]
    public async Task ScheduleDispatchTask_Should_Use_Compensation_Lane_When_Dispatch_Is_Compensation()
    {
        var mule = new CapturingMuleClient();
        var scheduler = new MuleRuntimeDurableWorkScheduler(mule);
        var envelope = new RuntimeDispatchEnvelope
        {
            DispatchId = "dispatch-1",
            OrchestrationInstanceId = "instance-1",
            CorrelationId = "corr-1",
            EnvironmentKey = "local",
            OrchestrationName = "order.fulfillment",
            OrchestrationVersion = "1.0.0",
            StageKey = "payment",
            TaskKey = "compensate-payment",
            TaskExecutionId = "task-exec-1",
            Payload = JsonNode.Parse("""{"amount":10}""")!,
            Destination = new RuntimeTransportDescriptor
            {
                Kind = RuntimeTransportKind.Message,
                Address = "orders.payment.compensate"
            },
            Metadata =
            {
                ["compensationExecutionId"] = "compensation-1"
            }
        };

        await scheduler.ScheduleDispatchTask(envelope);

        Assert.Equal(RuntimeDurableWorkLanes.Compensation, mule.CapturedLane);
    }

    [Fact]
    public async Task ScheduleReconcile_Should_Use_Reconcile_Action_With_Metadata()
    {
        var mule = new CapturingMuleClient();
        var scheduler = new MuleRuntimeDurableWorkScheduler(mule);
        var request = new RuntimeReconcileRequest
        {
            ReconcileKey = "runtime-reconcile:123",
            DueOnUtc = new DateTime(2026, 8, 14, 1, 2, 3, DateTimeKind.Utc),
            RequestedBy = "test"
        };

        await scheduler.ScheduleReconcile(request);

        Assert.Equal(RuntimeDurableWorkActionKeys.Reconcile, mule.CapturedKey);
        Assert.Same(request, mule.CapturedPayload);
        Assert.Equal(RuntimeDurableWorkLanes.Reconcile, mule.CapturedLane);
        Assert.Equal("runtime-reconcile:123", mule.CapturedCorrelationId);
        Assert.Equal("runtime-reconcile:123", mule.CapturedDeduplicationKey);
        Assert.Equal("krackend.runtime.reconcile", mule.CapturedMetadata["runtime.action"]);
        Assert.Equal("2026-08-14T01:02:03.0000000Z", mule.CapturedMetadata["runtime.reconcile.dueOnUtc"]);
    }

    [Fact]
    public void AddMuleDurableWork_Should_Register_Runtime_Scheduler()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMuleClient, CapturingMuleClient>();

        services.AddKrackendSagasOrchestrationsMuleDurableWork();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<MuleRuntimeDurableWorkScheduler>(
            provider.GetRequiredService<IRuntimeDurableWorkScheduler>());
    }

    private sealed class CapturingMuleClient : IMuleClient
    {
        public ActionKey CapturedKey { get; private set; }

        public object CapturedPayload { get; private set; } = null!;

        public string CapturedCorrelationId { get; private set; } = string.Empty;

        public string CapturedLane { get; private set; } = string.Empty;

        public string CapturedDeduplicationKey { get; private set; } = string.Empty;

        public Dictionary<string, string> CapturedMetadata { get; } = new();

        public ValueTask<Guid> EnqueueAsync<TPayload>(
            ActionKey key,
            TPayload payload,
            CancellationToken cancellationToken = default)
        {
            CapturedKey = key;
            CapturedPayload = payload!;
            return ValueTask.FromResult(Guid.NewGuid());
        }

        public ValueTask<Guid> EnqueueAsync<TPayload>(
            ActionKey key,
            TPayload payload,
            Action<EnqueueOptions> configure,
            CancellationToken cancellationToken = default)
        {
            var options = new EnqueueOptions();
            configure?.Invoke(options);

            CapturedKey = key;
            CapturedPayload = payload!;
            CapturedLane = options.Lane;
            CapturedCorrelationId = options.CorrelationId;
            CapturedDeduplicationKey = options.DeduplicationKey;

            foreach (var item in options.Metadata)
                CapturedMetadata[item.Key] = item.Value;

            return ValueTask.FromResult(Guid.NewGuid());
        }
    }
}
