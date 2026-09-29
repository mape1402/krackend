namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

/// <summary>
/// Builds deterministic idempotency keys for runtime ingress envelopes.
/// </summary>
public static class RuntimeIngressIdempotency
{
    /// <summary>
    /// Builds the durable idempotency key for an ingress envelope.
    /// </summary>
    public static string Build(RuntimeIngressEnvelope envelope)
    {
        if (envelope == null)
            throw new ArgumentNullException(nameof(envelope));

        if (!string.IsNullOrWhiteSpace(envelope.IdempotencyKey))
            return envelope.IdempotencyKey;

        return envelope.Kind switch
        {
            RuntimeIngressKind.Trigger => BuildTriggerKey(envelope, GetTriggerMetadata(envelope)),
            RuntimeIngressKind.TaskResponse => BuildTaskResponseKey(envelope),
            _ => throw new InvalidOperationException($"Unsupported runtime ingress kind '{envelope.Kind}'.")
        };
    }

    private static string BuildTriggerKey(RuntimeIngressEnvelope envelope, OrchestrationTriggerMetadata triggerMetadata)
    {
        if (!string.IsNullOrWhiteSpace(triggerMetadata.IdempotencyKey))
            return triggerMetadata.IdempotencyKey;

        return Join(
            "trigger",
            envelope.OrchestrationName,
            envelope.OrchestrationVersion,
            FirstNonEmpty(envelope.Source?.MessageId, triggerMetadata.EventId),
            FirstNonEmpty(envelope.CorrelationId, triggerMetadata.CorrelationId));
    }

    private static string BuildTaskResponseKey(RuntimeIngressEnvelope envelope)
        => Join(
            "response",
            envelope.OrchestrationInstanceId,
            envelope.DispatchId,
            envelope.TaskExecutionId,
            envelope.Attempt.ToString(),
            envelope.Source?.MessageId);

    private static string Join(params string[] values)
        => string.Join("|", values.Select(x => string.IsNullOrWhiteSpace(x) ? "-" : x.Trim()));

    private static OrchestrationTriggerMetadata GetTriggerMetadata(RuntimeIngressEnvelope envelope)
    {
        if (envelope.Metadata is null ||
            !envelope.Metadata.TryGetValue(OrchestrationMetadataConstants.TriggerMetadataKey, out var payload))
        {
            return new OrchestrationTriggerMetadata();
        }

        return OrchestrationTriggerMetadata.FromJson(payload);
    }

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
