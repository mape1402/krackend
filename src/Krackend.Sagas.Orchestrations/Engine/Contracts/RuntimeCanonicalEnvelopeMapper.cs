namespace Krackend.Sagas.Orchestrations.Engine;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;

/// <summary>
/// Maps current runtime commands into canonical ingress and dispatch envelopes.
/// </summary>
public static class RuntimeCanonicalEnvelopeMapper
{
    /// <summary>
    /// Maps a trigger intake item into a canonical ingress envelope.
    /// </summary>
    public static RuntimeIngressEnvelope ToIngressEnvelope(
        this TriggerIntakeBufferItem item,
        RuntimeTransportKind sourceTransport = RuntimeTransportKind.Message,
        string sourceAddress = null)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        return new RuntimeIngressEnvelope
        {
            IngressId = item.BufferItemId.ToString(),
            Kind = RuntimeIngressKind.Trigger,
            EnvironmentKey = item.EnvironmentKey,
            OrchestrationName = item.TriggerKey,
            OrchestrationVersion = item.ArtifactVersion,
            CorrelationId = item.CorrelationId,
            IdempotencyKey = item.IdempotencyKey,
            Payload = string.IsNullOrWhiteSpace(item.PayloadJson) ? null : JsonNode.Parse(item.PayloadJson),
            ReceivedOnUtc = item.ReceivedOnUtc,
            Source = new RuntimeTransportDescriptor
            {
                Kind = sourceTransport,
                Address = sourceAddress,
                MessageId = item.SourceMessageId
            }
        };
    }

    /// <summary>
    /// Maps a task response command into a canonical ingress envelope.
    /// </summary>
    public static RuntimeIngressEnvelope ToIngressEnvelope(
        this RuntimeMessageResponseCommand command,
        string environmentKey,
        string orchestrationName,
        string orchestrationVersion,
        RuntimeTransportKind sourceTransport = RuntimeTransportKind.Message,
        string sourceAddress = null,
        string sourceMessageId = null)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        return new RuntimeIngressEnvelope
        {
            Kind = RuntimeIngressKind.TaskResponse,
            EnvironmentKey = environmentKey,
            OrchestrationName = orchestrationName,
            OrchestrationVersion = orchestrationVersion,
            CorrelationId = command.CorrelationId,
            OrchestrationInstanceId = command.OrchestrationInstanceId,
            TaskExecutionId = command.TaskExecutionId,
            DispatchId = command.DispatchId,
            Payload = command.Payload,
            Source = new RuntimeTransportDescriptor
            {
                Kind = sourceTransport,
                Address = sourceAddress,
                MessageId = sourceMessageId
            }
        };
    }

    /// <summary>
    /// Maps a task dispatch request into a canonical dispatch envelope.
    /// </summary>
    public static RuntimeDispatchEnvelope ToDispatchEnvelope(this RuntimeTaskDispatchRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        return new RuntimeDispatchEnvelope
        {
            DispatchId = request.DispatchId,
            OrchestrationInstanceId = request.OrchestrationInstanceId,
            ExecutionKey = request.CommandId,
            CorrelationId = request.CorrelationId,
            OrchestrationName = request.OrchestrationDefinitionKey,
            OrchestrationVersion = request.OrchestrationVersion,
            StageKey = request.StageKey,
            TaskKey = request.TaskKey,
            TaskExecutionId = request.TaskExecutionId,
            Attempt = request.Attempt,
            Payload = request.Payload,
            CreatedOnUtc = request.StartedOnUtc,
            Destination = new RuntimeTransportDescriptor
            {
                Kind = MapTransportKind(request.DispatchType),
                Address = request.Destination,
                Version = string.IsNullOrWhiteSpace(request.MessageVersion) ? "1.0.0" : request.MessageVersion
            }
        };
    }

    private static RuntimeTransportKind MapTransportKind(string dispatchType)
        => string.Equals(dispatchType, "Messaging", StringComparison.OrdinalIgnoreCase)
            ? RuntimeTransportKind.Message
            : string.Equals(dispatchType, "Http", StringComparison.OrdinalIgnoreCase)
                ? RuntimeTransportKind.Http
                : RuntimeTransportKind.Unknown;
}
