namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;
using System.Text.Json.Nodes;

public sealed class RuntimeIngressIdempotencyTests
{
    [Fact]
    public void Build_WhenExplicitKeyExists_ReturnsIt()
    {
        var envelope = new RuntimeIngressEnvelope
        {
            IdempotencyKey = "explicit-key",
            Kind = RuntimeIngressKind.Trigger,
            OrchestrationName = "sales.sale.created"
        };

        var result = RuntimeIngressIdempotency.Build(envelope);

        Assert.Equal("explicit-key", result);
    }

    [Fact]
    public void Build_WhenTriggerEnvelopeHasNoExplicitKey_UsesTriggerFields()
    {
        var envelope = new RuntimeIngressEnvelope
        {
            Kind = RuntimeIngressKind.Trigger,
            OrchestrationName = " sales.sale.created ",
            OrchestrationVersion = " 1.0.0 ",
            CorrelationId = " correlation-1 ",
            Source = new RuntimeTransportDescriptor
            {
                MessageId = " message-1 "
            }
        };

        var result = RuntimeIngressIdempotency.Build(envelope);

        Assert.Equal("trigger|sales.sale.created|1.0.0|message-1|correlation-1", result);
    }

    [Fact]
    public void Build_WhenTriggerMetadataHasIdempotencyKey_UsesIt()
    {
        var envelope = new RuntimeIngressEnvelope
        {
            Kind = RuntimeIngressKind.Trigger,
            OrchestrationName = "sales.sale.created",
            Metadata =
            {
                [OrchestrationMetadataConstants.TriggerMetadataKey] = new OrchestrationTriggerMetadata
                {
                    IdempotencyKey = "trigger-idem-1",
                    EventId = "event-1",
                    CorrelationId = "correlation-1"
                }.ToJson()
            }
        };

        var result = RuntimeIngressIdempotency.Build(envelope);

        Assert.Equal("trigger-idem-1", result);
    }

    [Fact]
    public void Build_WhenTriggerEnvelopeIsMissingSourceFields_UsesTriggerMetadata()
    {
        var envelope = new RuntimeIngressEnvelope
        {
            Kind = RuntimeIngressKind.Trigger,
            OrchestrationName = "sales.sale.created",
            OrchestrationVersion = "1.0.0",
            Metadata =
            {
                [OrchestrationMetadataConstants.TriggerMetadataKey] = JsonNode.Parse(
                    """{"EventId":"event-1","CorrelationId":"correlation-1"}""")!
            }
        };

        var result = RuntimeIngressIdempotency.Build(envelope);

        Assert.Equal("trigger|sales.sale.created|1.0.0|event-1|correlation-1", result);
    }

    [Fact]
    public void Build_WhenTaskResponseHasMissingOptionalFields_UsesPlaceholderSegments()
    {
        var envelope = new RuntimeIngressEnvelope
        {
            Kind = RuntimeIngressKind.TaskResponse,
            OrchestrationName = "sales.sale.created",
            OrchestrationInstanceId = "instance-1",
            DispatchId = "",
            TaskExecutionId = "task-1",
            Attempt = 2
        };

        var result = RuntimeIngressIdempotency.Build(envelope);

        Assert.Equal("response|instance-1|-|task-1|2|-", result);
    }

    [Fact]
    public void Build_WhenEnvelopeIsNullOrKindUnsupported_FailsFast()
    {
        Assert.Throws<ArgumentNullException>(() => RuntimeIngressIdempotency.Build(null!));

        var envelope = new RuntimeIngressEnvelope
        {
            Kind = (RuntimeIngressKind)999,
            OrchestrationName = "sales.sale.created"
        };

        Assert.Throws<InvalidOperationException>(() => RuntimeIngressIdempotency.Build(envelope));
    }
}
