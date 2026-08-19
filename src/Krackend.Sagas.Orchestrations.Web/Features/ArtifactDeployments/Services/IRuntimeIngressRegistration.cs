using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Registers runtime ingress bindings without exposing a concrete transport implementation.
/// </summary>
public interface IRuntimeIngressRegistration
{
    /// <summary>
    /// Gets whether this registrar can handle the binding.
    /// </summary>
    /// <param name="binding">Runtime messaging ingress binding.</param>
    /// <returns>True when the binding can be handled.</returns>
    bool CanHandle(RuntimeMessagingIngressBinding binding);

    /// <summary>
    /// Registers one ingress binding.
    /// </summary>
    /// <param name="artifact">Runtime artifact.</param>
    /// <param name="binding">Runtime ingress binding.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Register(
        RuntimeOrchestrationArtifact artifact,
        RuntimeMessagingIngressBinding binding,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes one ingress binding.
    /// </summary>
    /// <param name="artifact">Runtime artifact.</param>
    /// <param name="binding">Runtime ingress binding.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Remove(
        RuntimeOrchestrationArtifact artifact,
        RuntimeMessagingIngressBinding binding,
        CancellationToken cancellationToken = default);
}
