using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Connects runtime ingress bindings without exposing a concrete transport implementation.
/// </summary>
public interface IRuntimeIngressConnector
{
    /// <summary>
    /// Gets whether this connector can handle the binding.
    /// </summary>
    /// <param name="binding">Runtime messaging ingress binding.</param>
    /// <returns>True when the binding can be handled.</returns>
    bool CanHandle(RuntimeMessagingIngressBinding binding);

    /// <summary>
    /// Connects one ingress binding.
    /// </summary>
    /// <param name="artifact">Runtime artifact.</param>
    /// <param name="binding">Runtime ingress binding.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Connect(
        RuntimeOrchestrationArtifact artifact,
        RuntimeMessagingIngressBinding binding,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects one ingress binding.
    /// </summary>
    /// <param name="artifact">Runtime artifact.</param>
    /// <param name="binding">Runtime ingress binding.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Disconnect(
        RuntimeOrchestrationArtifact artifact,
        RuntimeMessagingIngressBinding binding,
        CancellationToken cancellationToken = default);
}
