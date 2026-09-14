using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Issues short-lived access tokens for inbound Design distribution calls.
/// </summary>
public interface IControlPlaneConnectionTokenIssuer
{
    /// <summary>
    /// Issues a token after validating a Design-owned inbound credential.
    /// </summary>
    /// <param name="request">Token request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Issued token response.</returns>
    Task<ConnectionTokenResponse> IssueAsync(ConnectionTokenRequest request, CancellationToken cancellationToken = default);
}
