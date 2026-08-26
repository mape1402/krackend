using Microsoft.AspNetCore.Http;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Default runtime authenticator for control-plane artifact push requests.
/// </summary>
public sealed class RuntimeArtifactDeliveryEndpointAuthenticator : IRuntimeArtifactDeliveryEndpointAuthenticator
{
    private readonly IRuntimeConnectionTokenValidator _tokenValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeArtifactDeliveryEndpointAuthenticator"/> class.
    /// </summary>
    public RuntimeArtifactDeliveryEndpointAuthenticator(IRuntimeConnectionTokenValidator tokenValidator)
    {
        _tokenValidator = tokenValidator ?? throw new ArgumentNullException(nameof(tokenValidator));
    }

    /// <inheritdoc />
    public async Task<RuntimeArtifactDeliveryEndpointAuthenticationResult> AuthenticateAsync(
        HttpRequest request,
        string body,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = ReadBearerToken(request);
        var validation = await _tokenValidator.ValidateAsync(
            token,
            [ArtifactDeliveryScope.ArtifactPush],
            cancellationToken);

        return validation.Succeeded
            ? RuntimeArtifactDeliveryEndpointAuthenticationResult.Success(validation.Principal.NodeKey)
            : RuntimeArtifactDeliveryEndpointAuthenticationResult.Failure(validation.Message);
    }

    private static string ReadBearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : string.Empty;
    }
}
