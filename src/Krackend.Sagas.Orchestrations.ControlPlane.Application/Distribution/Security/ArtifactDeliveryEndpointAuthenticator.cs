using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Microsoft.AspNetCore.Http;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Default control-plane authenticator for runtime artifact pull requests.
/// </summary>
public sealed class ArtifactDeliveryEndpointAuthenticator : IArtifactDeliveryEndpointAuthenticator
{
    private readonly IControlPlaneConnectionTokenValidator _tokenValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactDeliveryEndpointAuthenticator"/> class.
    /// </summary>
    public ArtifactDeliveryEndpointAuthenticator(IControlPlaneConnectionTokenValidator tokenValidator)
    {
        _tokenValidator = tokenValidator ?? throw new ArgumentNullException(nameof(tokenValidator));
    }

    /// <inheritdoc />
    public async Task<ArtifactDeliveryEndpointAuthenticationResult> AuthenticateRuntimeNodeAsync(
        HttpRequest request,
        string runtimeNodeId,
        string body,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _tokenValidator.ValidateAsync(
            ReadBearerToken(request),
            runtimeNodeId,
            ResolveRequiredScopes(request),
            cancellationToken);

        return validation.Succeeded
            ? ArtifactDeliveryEndpointAuthenticationResult.Success()
            : ArtifactDeliveryEndpointAuthenticationResult.Failure(validation.Message);
    }

    private static IReadOnlyCollection<ArtifactDeliveryScope> ResolveRequiredScopes(HttpRequest request)
    {
        if (HttpMethods.IsPost(request.Method))
        {
            return [ArtifactDeliveryScope.ArtifactAcknowledge];
        }

        return request.Path.Value?.Contains("/pending", StringComparison.OrdinalIgnoreCase) == true
            ? [ArtifactDeliveryScope.ReleaseRead]
            : [ArtifactDeliveryScope.ArtifactRead];
    }

    private static string ReadBearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : string.Empty;
    }
}
