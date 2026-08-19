using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;
using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Consuming;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;
using Krackend.Sagas.Orchestrations.Web;
using Microsoft.Extensions.Options;
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
            new EmptyArtifactCatalog(),
            new RuntimeArtifactIngressBindingBuilder(),
            new IRuntimeIngressRegistration[]
            {
                new MessageIngressRegistration(scheduler, new NoopBackChannelResponseHandler(), new[] { registry })
            });

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

    [Fact]
    public async Task SynchronizeActiveArtifacts_ReadsArtifactsInPages()
    {
        var catalog = new CapturingPagedArtifactCatalog(CreateArtifact("first"), CreateArtifact("second"));
        var registry = new CapturingMessageConsumerRegistry();
        var synchronizer = new RuntimeArtifactConsumerSynchronizer(
            catalog,
            new RuntimeArtifactIngressBindingBuilder(),
            new IRuntimeIngressRegistration[]
            {
                new MessageIngressRegistration(new CapturingDurableWorkScheduler(), new NoopBackChannelResponseHandler(), new[] { registry })
            },
            Options.Create(new RuntimeIngressSynchronizationOptions { ActiveArtifactPageSize = 1 }));

        await synchronizer.SynchronizeActiveArtifacts();

        Assert.Equal(2, catalog.Reads);
        Assert.Equal(new[] { 0, 1 }, catalog.Offsets);
        Assert.Equal(4, registry.Registered.Count);
    }

    [Fact]
    public async Task SynchronizeArtifact_DoesNotRegisterSameArtifactBindingTwice()
    {
        var artifact = CreateArtifact();
        var registry = new CapturingMessageConsumerRegistry();
        var synchronizer = new RuntimeArtifactConsumerSynchronizer(
            new EmptyArtifactCatalog(),
            new RuntimeArtifactIngressBindingBuilder(),
            new IRuntimeIngressRegistration[]
            {
                new MessageIngressRegistration(new CapturingDurableWorkScheduler(), new NoopBackChannelResponseHandler(), new[] { registry })
            });

        await synchronizer.SynchronizeArtifact(artifact);
        await synchronizer.SynchronizeArtifact(artifact);

        Assert.Equal(2, registry.Registered.Count);
        Assert.Equal(2, registry.Removed.Count);
    }

    private static RuntimeOrchestrationArtifact CreateArtifact(string orchestrationKey = "order.fulfillment")
        => new()
        {
            Id = Id.New(),
            EnvironmentKey = "local",
            OrchestrationDefinitionKey = orchestrationKey,
            ArtifactType = "orchestration.deploy",
            SourceOrchestrationVersionId = Id.New(),
            Version = new SemanticVersion(2, 1, 0),
            ArtifactChecksum = new Checksum("checksum"),
            ArtifactPayload = new JsonObject
            {
                ["Key"] = orchestrationKey,
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
                ["StageDefinitions"] = new JsonArray()
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

        public ValueTask<Guid> ScheduleReconcile(RuntimeReconcileRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class CapturingMessageConsumerRegistry : IMessageConsumerRegistry
    {
        public MessageConsumerRegistration TriggerRegistration { get; private set; } = null!;
        public List<MessageConsumerRegistration> Registered { get; } = new();
        public List<string> Removed { get; } = new();

        public Task Register(MessageConsumerRegistration registration, CancellationToken cancellationToken = default)
        {
            Registered.Add(registration);
            if (registration.Topic == "orders.created")
                TriggerRegistration = registration;

            return Task.CompletedTask;
        }

        public Task Remove(string topic, string version, CancellationToken cancellationToken = default)
        {
            Removed.Add($"{topic}:{version}");
            return Task.CompletedTask;
        }
    }

    private sealed class NoopBackChannelResponseHandler : IRuntimeBackChannelResponseHandler
    {
        public Task Handle(MessageConsumeContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class EmptyArtifactCatalog : IRuntimeArtifactCatalog
    {
        public Task<RuntimeArtifactPage> ReadActiveDeployments(RuntimeArtifactPageCursor cursor, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult(new RuntimeArtifactPage(Array.Empty<RuntimeOrchestrationArtifact>(), null!, false));
    }

    private sealed class CapturingPagedArtifactCatalog : IRuntimeArtifactCatalog
    {
        private readonly RuntimeOrchestrationArtifact[] _artifacts;

        public CapturingPagedArtifactCatalog(params RuntimeOrchestrationArtifact[] artifacts)
        {
            _artifacts = artifacts;
        }

        public int Reads { get; private set; }

        public List<int> Offsets { get; } = new();

        public Task<RuntimeArtifactPage> ReadActiveDeployments(RuntimeArtifactPageCursor cursor, int pageSize, CancellationToken cancellationToken = default)
        {
            Reads++;
            Offsets.Add(cursor.Offset);
            var items = _artifacts.Skip(cursor.Offset).Take(pageSize).ToArray();
            var nextOffset = cursor.Offset + items.Length;
            var hasMore = nextOffset < _artifacts.Length;
            return Task.FromResult(new RuntimeArtifactPage(
                items,
                hasMore ? new RuntimeArtifactPageCursor(nextOffset) : null!,
                hasMore));
        }
    }
}
