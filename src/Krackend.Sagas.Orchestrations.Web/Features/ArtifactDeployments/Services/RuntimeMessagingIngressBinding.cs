namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Describes a broker-neutral messaging ingress binding derived from an artifact.
/// </summary>
public sealed class RuntimeMessagingIngressBinding
{
    /// <summary>
    /// Gets artifact id.
    /// </summary>
    public required string ArtifactId { get; init; }

    /// <summary>
    /// Gets orchestration key.
    /// </summary>
    public required string OrchestrationKey { get; init; }

    /// <summary>
    /// Gets orchestration artifact version.
    /// </summary>
    public required string OrchestrationVersion { get; init; }

    /// <summary>
    /// Gets ingress binding kind.
    /// </summary>
    public RuntimeIngressBindingKind Kind { get; init; }

    /// <summary>
    /// Gets topic or queue.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// Gets message version used for consumer registration.
    /// </summary>
    public required string Version { get; init; }
}
