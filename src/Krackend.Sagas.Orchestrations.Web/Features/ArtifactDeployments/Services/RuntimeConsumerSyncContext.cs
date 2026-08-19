using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Consuming;

namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Carries the runtime artifact consumer synchronization state across lifecycle operations.
/// </summary>
internal sealed class RuntimeConsumerSyncContext
{
    /// <summary>
    /// Gets the registry that receives consumer changes.
    /// </summary>
    public required IMessageConsumerRegistry Registry { get; init; }

    /// <summary>
    /// Gets the runtime artifact being synchronized.
    /// </summary>
    public required RuntimeOrchestrationArtifact Artifact { get; init; }

    /// <summary>
    /// Gets the consumer bindings derived from the artifact payload.
    /// </summary>
    public required RuntimeArtifactIngressBindingSet Bindings { get; init; }
}
