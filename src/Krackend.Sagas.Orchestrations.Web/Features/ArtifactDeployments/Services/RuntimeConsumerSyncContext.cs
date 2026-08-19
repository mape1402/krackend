using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Carries the runtime artifact consumer synchronization state across lifecycle operations.
/// </summary>
internal sealed class RuntimeConsumerSyncContext
{
    /// <summary>
    /// Gets the runtime artifact being synchronized.
    /// </summary>
    public required RuntimeOrchestrationArtifact Artifact { get; init; }

    /// <summary>
    /// Gets the consumer bindings derived from the artifact payload.
    /// </summary>
    public required RuntimeArtifactIngressBindingSet Bindings { get; init; }
}
