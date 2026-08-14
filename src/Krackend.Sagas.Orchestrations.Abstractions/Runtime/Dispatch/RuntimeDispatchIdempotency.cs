namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;

/// <summary>
/// Builds deterministic idempotency keys for runtime dispatch work.
/// </summary>
public static class RuntimeDispatchIdempotency
{
    /// <summary>
    /// Builds the durable idempotency key for a dispatch envelope.
    /// </summary>
    public static string Build(RuntimeDispatchEnvelope envelope)
    {
        if (envelope == null)
            throw new ArgumentNullException(nameof(envelope));

        return string.Join(
            "|",
            "dispatch",
            Clean(envelope.OrchestrationInstanceId),
            Clean(envelope.DispatchId),
            Clean(envelope.TaskExecutionId),
            envelope.Attempt.ToString());
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
}
