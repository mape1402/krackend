using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;
using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Consuming;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;
using Krackend.Sagas.Orchestrations.Web;

namespace Krackend.Sagas.Orchestrations.Tests.Web;

public sealed class RuntimeBackChannelResponseHandlerTests
{
    [Fact]
    public async Task Handle_Should_Schedule_Response_Ingress_With_Trace_Metadata()
    {
        var scheduler = new CapturingDurableWorkScheduler();
        var handler = new RuntimeBackChannelResponseHandler(scheduler);

        await handler.Handle(CreateContext());

        var envelope = Assert.IsType<RuntimeIngressEnvelope>(scheduler.ProcessIngressEnvelope);
        Assert.Equal(RuntimeIngressKind.TaskResponse, envelope.Kind);
        Assert.Equal("local", envelope.EnvironmentKey);
        Assert.Equal("order.fulfillment", envelope.OrchestrationName);
        Assert.Equal("2.1.0", envelope.OrchestrationVersion);
        Assert.Equal("instance-1", envelope.OrchestrationInstanceId);
        Assert.Equal("dispatch-1", envelope.DispatchId);
        Assert.Equal("task-exec-1", envelope.TaskExecutionId);
        Assert.Equal("corr-1", envelope.CorrelationId);
        Assert.Equal("saga-1", envelope.SagaId);
        Assert.Equal(4, envelope.Attempt);
        Assert.Equal(RuntimeTransportKind.Message, envelope.Source.Kind);
        Assert.Equal("orders.backchannel", envelope.Source.Address);
        Assert.Equal("2.1.0", envelope.Source.Version);
        Assert.Equal("dispatch-1", envelope.Source.MessageId);
        Assert.Equal("paid", envelope.Payload["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Handle_Should_Reject_Response_Without_Dispatch_Id()
    {
        var scheduler = new CapturingDurableWorkScheduler();
        var handler = new RuntimeBackChannelResponseHandler(scheduler);
        var context = CreateContext(dispatchId: "");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(context));

        Assert.Equal("Back-channel response metadata must include dispatch id.", ex.Message);
    }

    private static MessageConsumeContext CreateContext(string dispatchId = "dispatch-1")
        => new()
        {
            Topic = "orders.backchannel",
            Version = "2.1.0",
            CreatedOnUtc = new DateTimeOffset(2026, 8, 14, 1, 2, 3, TimeSpan.Zero),
            Message = JsonNode.Parse("""{"status":"paid"}"""),
            Metadata = new OrchestratorMessageMetadata
            {
                SagaId = "saga-1",
                OrchestrationId = "orchestration-1",
                OrchestrationKey = "order.fulfillment",
                OrchestrationVersion = "2.1.0",
                OrchestrationInstanceId = "instance-1",
                TaskExecutionId = "task-exec-1",
                DispatchId = dispatchId,
                CorrelationId = "corr-1",
                ResponseTopic = "orders.backchannel",
                ResponseVersion = "2.1.0",
                Environment = "local",
                CurrentState = new OrchestrationRuntimeState
                {
                    Status = "Waiting",
                    CurrentStageKey = "payment",
                    CurrentTaskKey = "charge-payment",
                    Attempt = 4,
                    StartedOnUtc = new DateTime(2026, 8, 14, 1, 2, 0, DateTimeKind.Utc),
                    UpdatedOnUtc = new DateTime(2026, 8, 14, 1, 2, 2, DateTimeKind.Utc)
                }
            }
        };

    private sealed class CapturingDurableWorkScheduler : IRuntimeDurableWorkScheduler
    {
        public object ProcessIngressEnvelope { get; private set; } = null!;

        public ValueTask<Guid> ScheduleProcessIngress(RuntimeIngressEnvelope envelope, CancellationToken cancellationToken = default)
        {
            ProcessIngressEnvelope = envelope;
            return ValueTask.FromResult(Guid.NewGuid());
        }

        public ValueTask<Guid> ScheduleDispatchTask(RuntimeDispatchEnvelope envelope, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public ValueTask<Guid> ScheduleReconcile(RuntimeReconcileRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
