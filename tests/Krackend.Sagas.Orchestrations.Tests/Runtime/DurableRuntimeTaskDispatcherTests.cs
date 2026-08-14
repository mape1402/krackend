using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;
using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.Engine.DurableWork;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class DurableRuntimeTaskDispatcherTests
{
    [Fact]
    public async Task Dispatch_Should_Schedule_Dispatch_Task_Durable_Work()
    {
        var scheduler = new CapturingDurableWorkScheduler();
        var dispatcher = new DurableRuntimeTaskDispatcher(scheduler);

        var result = await dispatcher.Dispatch(CreateRequest());

        Assert.True(result.Succeeded);
        Assert.Equal("Scheduled", result.Status);
        Assert.NotEqual(Guid.Empty.ToString(), result.ExternalReference);

        var envelope = scheduler.DispatchEnvelope;
        Assert.Equal("dispatch-1", envelope.DispatchId);
        Assert.Equal("local", envelope.EnvironmentKey);
        Assert.Equal("orders.payment", envelope.Destination.Address);
    }

    private static RuntimeTaskDispatchRequest CreateRequest()
        => new()
        {
            CommandId = "cmd-1",
            CorrelationId = "corr-1",
            TaskKind = "Messaging",
            DispatchType = "Messaging",
            Destination = "orders.payment",
            MessageVersion = "1.0.0",
            Payload = JsonNode.Parse("""{"amount":10}""")!,
            OrchestrationDefinitionKey = "order.fulfillment",
            OrchestrationVersion = "2.0.0",
            OrchestrationInstanceId = "instance-1",
            TaskExecutionId = "task-exec-1",
            DispatchId = "dispatch-1",
            EnvironmentKey = "local",
            StageKey = "payment",
            TaskKey = "charge-payment",
            CurrentStatus = "Running",
            Attempt = 1,
            StartedOnUtc = new DateTime(2026, 8, 14, 1, 2, 3, DateTimeKind.Utc),
            UpdatedOnUtc = new DateTime(2026, 8, 14, 1, 2, 4, DateTimeKind.Utc)
        };

    private sealed class CapturingDurableWorkScheduler : IRuntimeDurableWorkScheduler
    {
        public RuntimeDispatchEnvelope DispatchEnvelope { get; private set; } = null!;

        public ValueTask<Guid> ScheduleProcessIngress(Abstractions.Runtime.Ingress.RuntimeIngressEnvelope envelope, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public ValueTask<Guid> ScheduleDispatchTask(RuntimeDispatchEnvelope envelope, CancellationToken cancellationToken = default)
        {
            DispatchEnvelope = envelope;
            return ValueTask.FromResult(Guid.NewGuid());
        }

        public ValueTask<Guid> ScheduleReconcile(RuntimeReconcileRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
