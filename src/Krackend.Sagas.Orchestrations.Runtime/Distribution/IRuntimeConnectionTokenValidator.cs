using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Validates bearer tokens presented to Runtime distribution endpoints.
/// </summary>
public interface IRuntimeConnectionTokenValidator
{
    /// <summary>
    /// Validates a bearer token and required scopes.
    /// </summary>
    /// <param name="token">Bearer token.</param>
    /// <param name="requiredScopes">Required scopes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Token validation result.</returns>
    Task<ConnectionTokenValidationResult> ValidateAsync(
        string token,
        IReadOnlyCollection<ArtifactDeliveryScope> requiredScopes,
        CancellationToken cancellationToken = default);
}
