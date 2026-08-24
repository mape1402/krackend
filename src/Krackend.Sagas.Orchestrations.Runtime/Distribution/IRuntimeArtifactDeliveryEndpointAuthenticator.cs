using Microsoft.AspNetCore.Http;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Authenticates control-plane artifact delivery requests sent to the runtime.
/// </summary>
public interface IRuntimeArtifactDeliveryEndpointAuthenticator
{
    /// <summary>
    /// Authenticates a signed control-plane request.
    /// </summary>
    Task<RuntimeArtifactDeliveryEndpointAuthenticationResult> AuthenticateAsync(
        HttpRequest request,
        string body,
        CancellationToken cancellationToken = default);
}
