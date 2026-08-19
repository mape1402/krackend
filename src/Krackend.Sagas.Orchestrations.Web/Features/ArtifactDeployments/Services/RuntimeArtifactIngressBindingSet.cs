namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Describes all runtime ingress bindings derived from one runtime artifact.
/// </summary>
public sealed class RuntimeArtifactIngressBindingSet
{
    /// <summary>
    /// Gets orchestration key.
    /// </summary>
    public required string OrchestrationKey { get; init; }

    /// <summary>
    /// Gets orchestration artifact version.
    /// </summary>
    public required string OrchestrationVersion { get; init; }

    /// <summary>
    /// Gets back-channel messaging binding.
    /// </summary>
    public required RuntimeMessagingIngressBinding BackChannel { get; init; }

    /// <summary>
    /// Gets messaging trigger bindings.
    /// </summary>
    public IReadOnlyCollection<RuntimeMessagingIngressBinding> MessagingTriggers { get; init; } = Array.Empty<RuntimeMessagingIngressBinding>();

    /// <summary>
    /// Gets skipped ingress bindings.
    /// </summary>
    public IReadOnlyCollection<RuntimeSkippedIngressBinding> Skipped { get; init; } = Array.Empty<RuntimeSkippedIngressBinding>();
}
