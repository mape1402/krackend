namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Describes an artifact ingress that was intentionally skipped.
/// </summary>
public sealed class RuntimeSkippedIngressBinding
{
    /// <summary>
    /// Gets orchestration key.
    /// </summary>
    public required string OrchestrationKey { get; init; }

    /// <summary>
    /// Gets skipped ingress reason.
    /// </summary>
    public required string Reason { get; init; }
}
