namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;

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
            RuntimeIngressKind.Trigger => BuildTriggerKey(envelope),
            RuntimeIngressKind.TaskResponse => BuildTaskResponseKey(envelope),
            _ => throw new InvalidOperationException($"Unsupported runtime ingress kind '{envelope.Kind}'.")
        };
    }

    private static string BuildTriggerKey(RuntimeIngressEnvelope envelope)
        => Join(
            "trigger",
            envelope.EnvironmentKey,
            envelope.OrchestrationName,
            envelope.OrchestrationVersion,
            envelope.Source?.MessageId,
            envelope.CorrelationId);

    private static string BuildTaskResponseKey(RuntimeIngressEnvelope envelope)
        => Join(
            "response",
            envelope.EnvironmentKey,
            envelope.OrchestrationInstanceId,
            envelope.DispatchId,
            envelope.TaskExecutionId,
            envelope.Attempt.ToString(),
            envelope.Source?.MessageId);

    private static string Join(params string[] values)
        => string.Join("|", values.Select(x => string.IsNullOrWhiteSpace(x) ? "-" : x.Trim()));
}
