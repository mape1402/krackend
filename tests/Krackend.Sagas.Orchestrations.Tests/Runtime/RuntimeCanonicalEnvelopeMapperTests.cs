using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;
using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeCanonicalEnvelopeMapperTests
{
    [Fact]
    public void TriggerIntakeItem_Should_Map_To_Canonical_Ingress_Envelope()
    {
        var item = new TriggerIntakeBufferItem
        {
            BufferItemId = Id.New(),
            EnvironmentKey = "local",
            TriggerKey = "order.fulfillment",
            ArtifactVersion = "2.0.0",
            CorrelationId = "corr-1",
            IdempotencyKey = "idem-1",
            SourceMessageId = "msg-1",
            PayloadJson = """{"orderId":"A1"}""",
            ReceivedOnUtc = new DateTime(2026, 8, 14, 1, 2, 3, DateTimeKind.Utc)
        };

        var envelope = item.ToIngressEnvelope(RuntimeTransportKind.Message, "orders.trigger");

        Assert.Equal(RuntimeIngressKind.Trigger, envelope.Kind);
        Assert.Equal("local", envelope.EnvironmentKey);
        Assert.Equal("order.fulfillment", envelope.OrchestrationName);
        Assert.Equal("2.0.0", envelope.OrchestrationVersion);
        Assert.Equal("corr-1", envelope.CorrelationId);
        Assert.Equal("idem-1", RuntimeIngressIdempotency.Build(envelope));
        Assert.Equal(RuntimeTransportKind.Message, envelope.Source.Kind);
        Assert.Equal("orders.trigger", envelope.Source.Address);
        Assert.Equal("msg-1", envelope.Source.MessageId);
        Assert.Equal("A1", envelope.Payload["orderId"]!.GetValue<string>());
    }

    [Fact]
    public void TaskResponse_Should_Build_Deterministic_Idempotency_Key()
    {
        var command = new RuntimeMessageResponseCommand
        {
            OrchestrationInstanceId = "instance-1",
            TaskExecutionId = "task-exec-1",
            DispatchId = "dispatch-1",
            CorrelationId = "corr-1",
            Payload = JsonNode.Parse("""{"ok":true}""")
        };

        var envelope = command.ToIngressEnvelope(
            "local",
            "order.fulfillment",
            "1.0.0",
            sourceMessageId: "response-msg-1");

        Assert.Equal(RuntimeIngressKind.TaskResponse, envelope.Kind);
        Assert.Equal("response|local|instance-1|dispatch-1|task-exec-1|0|response-msg-1", RuntimeIngressIdempotency.Build(envelope));
    }

    [Fact]
    public void TaskDispatchRequest_Should_Map_To_Canonical_Dispatch_Envelope()
    {
        var request = new RuntimeTaskDispatchRequest
        {
            CommandId = "cmd-1",
            CorrelationId = "corr-1",
            TaskKind = "Service",
            DispatchType = "Messaging",
            Destination = "orders.reserve",
            MessageVersion = "1.2.3",
            Payload = JsonNode.Parse("""{"id":10}""")!,
            OrchestrationDefinitionKey = "order.fulfillment",
            OrchestrationVersion = "1.0.0",
            OrchestrationInstanceId = "instance-1",
            TaskExecutionId = "task-exec-1",
            DispatchId = "dispatch-1",
            EnvironmentKey = "local",
            StageKey = "reserve",
            TaskKey = "reserve-stock",
            CurrentStatus = "Running",
            Attempt = 2,
            StartedOnUtc = new DateTime(2026, 8, 14, 1, 2, 3, DateTimeKind.Utc),
            UpdatedOnUtc = new DateTime(2026, 8, 14, 1, 2, 4, DateTimeKind.Utc)
        };

        var envelope = request.ToDispatchEnvelope();

        Assert.Equal("dispatch-1", envelope.DispatchId);
        Assert.Equal("local", envelope.EnvironmentKey);
        Assert.Equal(RuntimeTransportKind.Message, envelope.Destination.Kind);
        Assert.Equal("orders.reserve", envelope.Destination.Address);
        Assert.Equal("1.2.3", envelope.Destination.Version);
        Assert.Equal("dispatch|instance-1|dispatch-1|task-exec-1|2", RuntimeDispatchIdempotency.Build(envelope));
    }
}
