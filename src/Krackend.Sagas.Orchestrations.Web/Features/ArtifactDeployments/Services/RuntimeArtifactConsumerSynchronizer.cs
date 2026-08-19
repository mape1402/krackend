using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;
using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Consuming;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Registers or removes trigger and back-channel consumers when runtime artifacts are deployed, deprecated or archived.
/// </summary>
public sealed class RuntimeArtifactConsumerSynchronizer : IRuntimeArtifactConsumerSynchronizer, IRuntimeIngressSynchronizer
{
    private readonly IRuntimeArtifactCatalog _artifactCatalog;
    private readonly IRuntimeArtifactIngressBindingBuilder _bindingBuilder;
    private readonly IRuntimeDurableWorkScheduler _durableWorkScheduler;
    private readonly IRuntimeBackChannelResponseHandler _backChannelResponseHandler;
    private readonly IReadOnlyCollection<IMessageConsumerRegistry> _registries;
    private readonly RuntimeIngressSynchronizationOptions _options;
    private readonly ConcurrentDictionary<string, byte> _registeredBindings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeArtifactConsumerSynchronizer"/> class.
    /// </summary>
    /// <param name="artifactCatalog">Runtime artifact catalog.</param>
    /// <param name="bindingBuilder">Runtime ingress binding builder.</param>
    /// <param name="durableWorkScheduler">Runtime durable work scheduler used by trigger consumers.</param>
    /// <param name="backChannelResponseHandler">Handler used by response consumers.</param>
    /// <param name="registries">Available messaging consumer registries.</param>
    /// <param name="options">Synchronization options.</param>
    public RuntimeArtifactConsumerSynchronizer(
        IRuntimeArtifactCatalog artifactCatalog,
        IRuntimeArtifactIngressBindingBuilder bindingBuilder,
        IRuntimeDurableWorkScheduler durableWorkScheduler,
        IRuntimeBackChannelResponseHandler backChannelResponseHandler,
        IEnumerable<IMessageConsumerRegistry> registries,
        IOptions<RuntimeIngressSynchronizationOptions> options = null)
    {
        _artifactCatalog = artifactCatalog ?? throw new ArgumentNullException(nameof(artifactCatalog));
        _bindingBuilder = bindingBuilder ?? throw new ArgumentNullException(nameof(bindingBuilder));
        _durableWorkScheduler = durableWorkScheduler ?? throw new ArgumentNullException(nameof(durableWorkScheduler));
        _backChannelResponseHandler = backChannelResponseHandler ?? throw new ArgumentNullException(nameof(backChannelResponseHandler));
        _registries = registries?.ToArray() ?? Array.Empty<IMessageConsumerRegistry>();
        _options = options?.Value ?? new RuntimeIngressSynchronizationOptions();
    }

    /// <inheritdoc/>
    public async Task Synchronize(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default)
        => await SynchronizeArtifact(artifact, cancellationToken);

    /// <inheritdoc/>
    public async Task SynchronizeActiveArtifacts(CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Max(1, _options.ActiveArtifactPageSize);
        var cursor = RuntimeArtifactPageCursor.First;

        while (cursor is not null)
        {
            var page = await _artifactCatalog.ReadActiveDeployments(cursor, pageSize, cancellationToken);
            foreach (var artifact in page.Items)
                await SynchronizeArtifact(artifact, cancellationToken);

            cursor = page.HasMore ? page.NextCursor : null;
        }
    }

    /// <inheritdoc/>
    public async Task SynchronizeArtifact(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default)
    {
        if (artifact is null || _registries.Count == 0)
            return;

        var bindings = _bindingBuilder.Build(artifact);

        foreach (var registry in _registries)
            await ApplyLifecycle(new RuntimeConsumerSyncContext { Registry = registry, Artifact = artifact, Bindings = bindings }, cancellationToken);
    }

    private async Task ApplyLifecycle(RuntimeConsumerSyncContext context, CancellationToken cancellationToken)
    {
        var lifecycle = GetLifecycle(context.Artifact.ArtifactType);
        if (lifecycle == RuntimeArtifactLifecycle.Deploy)
        {
            await RegisterDeployConsumers(context, cancellationToken);
            return;
        }

        if (lifecycle == RuntimeArtifactLifecycle.Deprecated)
        {
            await RemoveTriggerConsumers(context, cancellationToken);
            return;
        }

        await RemoveTriggerConsumers(context, cancellationToken);
        UnmarkRegistered(context.Artifact, context.Bindings.BackChannel);
        await context.Registry.Remove(context.Bindings.BackChannel.Topic, context.Bindings.BackChannel.Version, cancellationToken);
    }

    private async Task RegisterDeployConsumers(RuntimeConsumerSyncContext context, CancellationToken cancellationToken)
    {
        foreach (var trigger in context.Bindings.MessagingTriggers)
            await RegisterTriggerConsumer(context, trigger, cancellationToken);

        await RegisterBackChannelConsumer(context, cancellationToken);
    }

    private async Task RegisterTriggerConsumer(RuntimeConsumerSyncContext context, RuntimeMessagingIngressBinding trigger, CancellationToken cancellationToken)
    {
        if (!TryMarkRegistered(context.Artifact, trigger))
            return;

        await context.Registry.Remove(trigger.Topic, trigger.Version, cancellationToken);
        await context.Registry.Register(CreateTriggerRegistration(context.Artifact, trigger), cancellationToken);
    }

    private async Task RegisterBackChannelConsumer(RuntimeConsumerSyncContext context, CancellationToken cancellationToken)
    {
        if (!TryMarkRegistered(context.Artifact, context.Bindings.BackChannel))
            return;

        await context.Registry.Remove(context.Bindings.BackChannel.Topic, context.Bindings.BackChannel.Version, cancellationToken);
        await context.Registry.Register(CreateBackChannelRegistration(context.Bindings), cancellationToken);
    }

    private async Task RemoveTriggerConsumers(RuntimeConsumerSyncContext context, CancellationToken cancellationToken)
    {
        foreach (var trigger in context.Bindings.MessagingTriggers)
        {
            UnmarkRegistered(context.Artifact, trigger);
            await context.Registry.Remove(trigger.Topic, trigger.Version, cancellationToken);
        }
    }

    private MessageConsumerRegistration CreateTriggerRegistration(RuntimeOrchestrationArtifact artifact, RuntimeMessagingIngressBinding trigger)
    {
        return new MessageConsumerRegistration
        {
            Topic = trigger.Topic,
            Version = trigger.Version,
            Handler = (context, token) => EnqueueTrigger(artifact, context, token)
        };
    }

    private MessageConsumerRegistration CreateBackChannelRegistration(RuntimeArtifactIngressBindingSet bindings)
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
        if (!ShouldProcessTrigger(artifact, context))
            return Task.CompletedTask;

        var envelope = new RuntimeIngressEnvelope
        {
            IngressId = Id.New().ToString(),
            Kind = RuntimeIngressKind.Trigger,
            EnvironmentKey = artifact.EnvironmentKey,
            OrchestrationName = artifact.OrchestrationDefinitionKey,
            OrchestrationVersion = artifact.Version.ToString(),
            CorrelationId = ResolveCorrelationId(context),
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

    private static bool ShouldProcessTrigger(RuntimeOrchestrationArtifact artifact, MessageConsumeContext context)
    {
        var requestedOrchestration = ReadPayloadString(context.Message, "OrchestrationKey", "orchestrationKey");
        return string.IsNullOrWhiteSpace(requestedOrchestration)
            || string.Equals(requestedOrchestration.Trim(), artifact.OrchestrationDefinitionKey, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveCorrelationId(MessageConsumeContext context)
        => FirstNonEmpty(
            context.Metadata?.CorrelationId,
            ReadPayloadString(context.Message, "OrderId", "orderId", "CorrelationId", "correlationId"),
            context.Metadata?.DispatchId,
            ReadPayloadString(context.Message, "MessageId", "messageId"));

    private static string BuildTriggerIdempotencyKey(MessageConsumeContext context)
    {
        var payloadKey = FirstNonEmpty(
            context.Metadata?.CorrelationId,
            ReadPayloadString(context.Message, "OrderId", "orderId", "CorrelationId", "correlationId"),
            context.Metadata?.DispatchId,
            ReadPayloadString(context.Message, "MessageId", "messageId"));

        if (!string.IsNullOrWhiteSpace(payloadKey))
            return payloadKey;

        return $"{context.Topic}|{context.Version}|{context.CreatedOnUtc.UtcTicks}|{Id.New()}";
    }

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static string ReadPayloadString(JsonNode node, params string[] names)
    {
        if (node is not JsonObject obj)
            return string.Empty;

        foreach (var name in names)
        {
            if (obj[name] is JsonValue value && value.TryGetValue<string>(out var text))
                return text ?? string.Empty;
        }

        return string.Empty;
    }

    private bool TryMarkRegistered(RuntimeOrchestrationArtifact artifact, RuntimeMessagingIngressBinding binding)
        => _registeredBindings.TryAdd(BuildRegistrationKey(artifact, binding), 0);

    private void UnmarkRegistered(RuntimeOrchestrationArtifact artifact, RuntimeMessagingIngressBinding binding)
        => _registeredBindings.TryRemove(BuildRegistrationKey(artifact, binding), out _);

    private static string BuildRegistrationKey(RuntimeOrchestrationArtifact artifact, RuntimeMessagingIngressBinding binding)
        => $"{artifact.Id}::{binding.OrchestrationKey}::{binding.OrchestrationVersion}::{binding.Kind}::{binding.Topic}::{binding.Version}";

    private static RuntimeArtifactLifecycle GetLifecycle(string artifactType)
    {
        if (string.Equals(artifactType, "orchestration.deprecate", StringComparison.OrdinalIgnoreCase))
            return RuntimeArtifactLifecycle.Deprecated;

        if (string.Equals(artifactType, "orchestration.archive", StringComparison.OrdinalIgnoreCase))
            return RuntimeArtifactLifecycle.Archived;

        return RuntimeArtifactLifecycle.Deploy;
    }
}
