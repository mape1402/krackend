using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Issues short-lived access tokens for inbound Runtime distribution calls.
/// </summary>
public interface IRuntimeConnectionTokenIssuer
{
    /// <summary>
    /// Issues a token after validating one Runtime-owned inbound credential.
    /// </summary>
    /// <param name="request">Token request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Issued token response.</returns>
    Task<ConnectionTokenResponse> IssueAsync(ConnectionTokenRequest request, CancellationToken cancellationToken = default);
}
