using Microsoft.AspNetCore.Http;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Authenticates runtime node requests sent to artifact delivery endpoints.
/// </summary>
public interface IArtifactDeliveryEndpointAuthenticator
{
    /// <summary>
    /// Authenticates a signed runtime node request.
    /// </summary>
    Task<ArtifactDeliveryEndpointAuthenticationResult> AuthenticateRuntimeNodeAsync(
        HttpRequest request,
        string runtimeNodeId,
        string body,
        CancellationToken cancellationToken = default);
}
