using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Validates bearer tokens presented to Design distribution endpoints.
/// </summary>
public interface IControlPlaneConnectionTokenValidator
{
    /// <summary>
    /// Validates a bearer token and required scopes.
    /// </summary>
    /// <param name="token">Bearer token.</param>
    /// <param name="runtimeNodeId">Runtime node id from the route.</param>
    /// <param name="requiredScopes">Required scopes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Token validation result.</returns>
    Task<ConnectionTokenValidationResult> ValidateAsync(
        string token,
        string runtimeNodeId,
        IReadOnlyCollection<ArtifactDeliveryScope> requiredScopes,
        CancellationToken cancellationToken = default);
}
