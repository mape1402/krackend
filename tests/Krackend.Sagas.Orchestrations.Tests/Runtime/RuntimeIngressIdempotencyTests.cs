namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;

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
