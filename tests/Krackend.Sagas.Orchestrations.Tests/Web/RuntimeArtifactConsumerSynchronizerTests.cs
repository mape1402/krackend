using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;
using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Consuming;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;
using Krackend.Sagas.Orchestrations.Web;
using SemanticVersion = Krackend.Sagas.Orchestrations.Abstractions.Primitives.SemanticVersion;

namespace Krackend.Sagas.Orchestrations.Tests.Web;

public sealed class RuntimeArtifactConsumerSynchronizerTests
{
    [Fact]
    public async Task TriggerConsumer_Should_Schedule_ProcessIngress_Durable_Work()
    {
        var scheduler = new CapturingDurableWorkScheduler();
        var registry = new CapturingMessageConsumerRegistry();
        var synchronizer = new RuntimeArtifactConsumerSynchronizer(
            scheduler,
            new NoopBackChannelResponseHandler(),
            new[] { registry });

        await synchronizer.Synchronize(CreateArtifact());
        await registry.TriggerRegistration.Handler(CreateConsumeContext(), CancellationToken.None);

        var envelope = Assert.IsType<RuntimeIngressEnvelope>(scheduler.ProcessIngressEnvelope);
        Assert.Equal(RuntimeIngressKind.Trigger, envelope.Kind);
        Assert.Equal("local", envelope.EnvironmentKey);
        Assert.Equal("order.fulfillment", envelope.OrchestrationName);
        Assert.Equal("2.1.0", envelope.OrchestrationVersion);
        Assert.Equal("corr-1", envelope.CorrelationId);
        Assert.Equal("saga-1", envelope.SagaId);
        Assert.Equal("corr-1", envelope.IdempotencyKey);
        Assert.Equal(RuntimeTransportKind.Message, envelope.Source.Kind);
        Assert.Equal("orders.created", envelope.Source.Address);
        Assert.Equal("1.0.0", envelope.Source.Version);
        Assert.Equal("A1", envelope.Payload["orderId"]!.GetValue<string>());
    }

    private static RuntimeOrchestrationArtifact CreateArtifact()
        => new()
        {
            Id = Id.New(),
            EnvironmentKey = "local",
            OrchestrationDefinitionKey = "order.fulfillment",
            ArtifactType = "orchestration.deploy",
            SourceOrchestrationVersionId = Id.New(),
            Version = new SemanticVersion(2, 1, 0),
            ArtifactChecksum = new Checksum("checksum"),
            ArtifactPayload = new JsonObject
            {
                ["TriggerBindings"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["IsEnabled"] = true,
                        ["TriggerChannel"] = new JsonObject
                        {
                            ["Topic"] = "orders.created",
                            ["Version"] = "1.0.0"
                        }
                    }
                },
                ["BackChannelTopic"] = "orders.backchannel"
            }
        };

    private static MessageConsumeContext CreateConsumeContext()
        => new()
        {
            Topic = "orders.created",
            Version = "1.0.0",
            CreatedOnUtc = new DateTimeOffset(2026, 8, 14, 1, 2, 3, TimeSpan.Zero),
            Message = JsonNode.Parse("""{"orderId":"A1"}"""),
            Metadata = new OrchestratorMessageMetadata
            {
                SagaId = "saga-1",
                OrchestrationId = "orchestration-1",
                OrchestrationKey = "order.fulfillment",
                OrchestrationVersion = "2.1.0",
                OrchestrationInstanceId = "instance-1",
                TaskExecutionId = "task-exec-1",
                DispatchId = "dispatch-1",
                CorrelationId = "corr-1",
                ResponseTopic = "orders.backchannel",
                ResponseVersion = "2.1.0",
                Environment = "local",
                CurrentState = new OrchestrationRuntimeState
                {
                    Status = "Pending",
                    CurrentStageKey = "reserve",
                    CurrentTaskKey = "reserve-stock",
                    Attempt = 1,
                    StartedOnUtc = new DateTime(2026, 8, 14, 1, 2, 3, DateTimeKind.Utc),
                    UpdatedOnUtc = new DateTime(2026, 8, 14, 1, 2, 3, DateTimeKind.Utc)
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
    }

    private sealed class CapturingMessageConsumerRegistry : IMessageConsumerRegistry
    {
        public MessageConsumerRegistration TriggerRegistration { get; private set; } = null!;

        public Task Register(MessageConsumerRegistration registration, CancellationToken cancellationToken = default)
        {
            if (registration.Topic == "orders.created")
                TriggerRegistration = registration;

            return Task.CompletedTask;
        }

        public Task Remove(string topic, string version, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoopBackChannelResponseHandler : IRuntimeBackChannelResponseHandler
    {
        public Task Handle(MessageConsumeContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
