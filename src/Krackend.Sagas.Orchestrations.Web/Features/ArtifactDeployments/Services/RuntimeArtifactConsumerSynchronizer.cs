using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;
using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Consuming;

namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Registers or removes trigger and back-channel consumers when runtime artifacts are deployed, deprecated or archived.
/// </summary>
public sealed class RuntimeArtifactConsumerSynchronizer : IRuntimeArtifactConsumerSynchronizer
{
    private readonly IRuntimeDurableWorkScheduler _durableWorkScheduler;
    private readonly IRuntimeBackChannelResponseHandler _backChannelResponseHandler;
    private readonly IReadOnlyCollection<IMessageConsumerRegistry> _registries;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeArtifactConsumerSynchronizer"/> class.
    /// </summary>
    /// <param name="durableWorkScheduler">Runtime durable work scheduler used by trigger consumers.</param>
    /// <param name="backChannelResponseHandler">Handler used by response consumers.</param>
    /// <param name="registries">Available messaging consumer registries.</param>
    public RuntimeArtifactConsumerSynchronizer(IRuntimeDurableWorkScheduler durableWorkScheduler, IRuntimeBackChannelResponseHandler backChannelResponseHandler, IEnumerable<IMessageConsumerRegistry> registries)
    {
        _durableWorkScheduler = durableWorkScheduler ?? throw new ArgumentNullException(nameof(durableWorkScheduler));
        _backChannelResponseHandler = backChannelResponseHandler ?? throw new ArgumentNullException(nameof(backChannelResponseHandler));
        _registries = registries?.ToArray() ?? Array.Empty<IMessageConsumerRegistry>();
    }

    /// <inheritdoc/>
    public async Task Synchronize(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default)
    {
        if (artifact is null || _registries.Count == 0)
            return;

        var bindings = RuntimeArtifactConsumerBindings.From(artifact);

        foreach (var registry in _registries)
            await ApplyLifecycle(new RuntimeConsumerSyncContext { Registry = registry, Artifact = artifact, Bindings = bindings }, cancellationToken);
    }

    private async Task ApplyLifecycle(RuntimeConsumerSyncContext context, CancellationToken cancellationToken)
    {
        if (context.Bindings.Lifecycle == RuntimeArtifactLifecycle.Deploy)
        {
            await RegisterDeployConsumers(context, cancellationToken);
            return;
        }

        if (context.Bindings.Lifecycle == RuntimeArtifactLifecycle.Deprecated)
        {
            await RemoveTriggerConsumers(context, cancellationToken);
            return;
        }

        await RemoveTriggerConsumers(context, cancellationToken);
        await context.Registry.Remove(context.Bindings.BackChannel.Topic, context.Bindings.BackChannel.Version, cancellationToken);
    }

    private async Task RegisterDeployConsumers(RuntimeConsumerSyncContext context, CancellationToken cancellationToken)
    {
        foreach (var trigger in context.Bindings.Triggers)
            await RegisterTriggerConsumer(context, trigger, cancellationToken);

        await context.Registry.Remove(context.Bindings.BackChannel.Topic, context.Bindings.BackChannel.Version, cancellationToken);
        await context.Registry.Register(CreateBackChannelRegistration(context.Bindings), cancellationToken);
    }

    private async Task RegisterTriggerConsumer(RuntimeConsumerSyncContext context, RuntimeArtifactConsumerBinding trigger, CancellationToken cancellationToken)
    {
        await context.Registry.Remove(trigger.Topic, trigger.Version, cancellationToken);
        await context.Registry.Register(CreateTriggerRegistration(context.Artifact, trigger), cancellationToken);
    }

    private async Task RemoveTriggerConsumers(RuntimeConsumerSyncContext context, CancellationToken cancellationToken)
    {
        foreach (var trigger in context.Bindings.Triggers)
            await context.Registry.Remove(trigger.Topic, trigger.Version, cancellationToken);
    }

    private MessageConsumerRegistration CreateTriggerRegistration(RuntimeOrchestrationArtifact artifact, RuntimeArtifactConsumerBinding trigger)
    {
        return new MessageConsumerRegistration
        {
            Topic = trigger.Topic,
            Version = trigger.Version,
            Handler = (context, token) => EnqueueTrigger(artifact, context, token)
        };
    }

    private MessageConsumerRegistration CreateBackChannelRegistration(RuntimeArtifactConsumerBindings bindings)
    {
        return new MessageConsumerRegistration
        {
            Topic = bindings.BackChannel.Topic,
            Version = bindings.BackChannel.Version,
            Handler = _backChannelResponseHandler.Handle
        };
    }

    private Task EnqueueTrigger(RuntimeOrchestrationArtifact artifact, MessageConsumeContext context, CancellationToken cancellationToken)
    {
        var envelope = new RuntimeIngressEnvelope
        {
            IngressId = Id.New().ToString(),
            Kind = RuntimeIngressKind.Trigger,
            EnvironmentKey = artifact.EnvironmentKey,
            OrchestrationName = artifact.OrchestrationDefinitionKey,
            OrchestrationVersion = artifact.Version.ToString(),
            CorrelationId = context.Metadata?.CorrelationId,
            SagaId = context.Metadata?.SagaId,
            IdempotencyKey = BuildTriggerIdempotencyKey(context),
            Payload = context.Message ?? new JsonObject(),
            ReceivedOnUtc = context.CreatedOnUtc.UtcDateTime,
            Source = new RuntimeTransportDescriptor
            {
                Kind = RuntimeTransportKind.Message,
                Address = context.Topic,
                Version = context.Version,
                MessageId = context.Metadata?.DispatchId ?? context.Metadata?.CorrelationId
            }
        };

        return _durableWorkScheduler.ScheduleProcessIngress(envelope, cancellationToken).AsTask();
    }

    private static string BuildTriggerIdempotencyKey(MessageConsumeContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.Metadata?.CorrelationId))
            return context.Metadata.CorrelationId;

        if (!string.IsNullOrWhiteSpace(context.Metadata?.DispatchId))
            return context.Metadata.DispatchId;

        return $"{context.Topic}|{context.Version}|{context.CreatedOnUtc.UtcTicks}|{Id.New()}";
    }
}
