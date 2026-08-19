using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;
using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Consuming;

namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Connects message-based runtime ingresses through the broker-neutral messaging registry.
/// </summary>
public sealed class MessageIngressConnector : IRuntimeIngressConnector
{
    private readonly IRuntimeDurableWorkScheduler _durableWorkScheduler;
    private readonly IRuntimeBackChannelResponseHandler _backChannelResponseHandler;
    private readonly IReadOnlyCollection<IMessageConsumerRegistry> _registries;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageIngressConnector"/> class.
    /// </summary>
    public MessageIngressConnector(
        IRuntimeDurableWorkScheduler durableWorkScheduler,
        IRuntimeBackChannelResponseHandler backChannelResponseHandler,
        IEnumerable<IMessageConsumerRegistry> registries)
    {
        _durableWorkScheduler = durableWorkScheduler ?? throw new ArgumentNullException(nameof(durableWorkScheduler));
        _backChannelResponseHandler = backChannelResponseHandler ?? throw new ArgumentNullException(nameof(backChannelResponseHandler));
        _registries = registries?.ToArray() ?? Array.Empty<IMessageConsumerRegistry>();
    }

    /// <inheritdoc/>
    public bool CanHandle(RuntimeMessagingIngressBinding binding)
        => binding is not null;

    /// <inheritdoc/>
    public async Task Connect(
        RuntimeOrchestrationArtifact artifact,
        RuntimeMessagingIngressBinding binding,
        CancellationToken cancellationToken = default)
    {
        if (artifact is null)
            throw new ArgumentNullException(nameof(artifact));

        if (binding is null)
            throw new ArgumentNullException(nameof(binding));

        foreach (var registry in _registries)
        {
            await registry.Remove(binding.Topic, binding.Version, cancellationToken);
            await registry.Register(CreateConsumer(artifact, binding), cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task Disconnect(
        RuntimeOrchestrationArtifact artifact,
        RuntimeMessagingIngressBinding binding,
        CancellationToken cancellationToken = default)
    {
        if (binding is null)
            return;

        foreach (var registry in _registries)
            await registry.Remove(binding.Topic, binding.Version, cancellationToken);
    }

    private MessageConsumerRegistration CreateConsumer(RuntimeOrchestrationArtifact artifact, RuntimeMessagingIngressBinding binding)
    {
        return new MessageConsumerRegistration
        {
            Topic = binding.Topic,
            Version = binding.Version,
            Handler = binding.Kind == RuntimeIngressBindingKind.BackChannel
                ? _backChannelResponseHandler.Handle
                : (context, token) => EnqueueTrigger(artifact, context, token)
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
}
