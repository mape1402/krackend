using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Default runtime authenticator for control-plane artifact push requests.
/// </summary>
public sealed class RuntimeArtifactDeliveryEndpointAuthenticator : IRuntimeArtifactDeliveryEndpointAuthenticator
{
    private readonly IControlPlaneDistributionSourceProvider _sourceProvider;
    private readonly IArtifactDeliverySecretResolver _secretResolver;
    private readonly IArtifactDeliverySignatureService _signatureService;
    private readonly IArtifactDeliveryNonceStore _nonceStore;
    private readonly ArtifactDeliverySecurityOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeArtifactDeliveryEndpointAuthenticator"/> class.
    /// </summary>
    public RuntimeArtifactDeliveryEndpointAuthenticator(
        IControlPlaneDistributionSourceProvider sourceProvider,
        IArtifactDeliverySecretResolver secretResolver,
        IArtifactDeliverySignatureService signatureService,
        IArtifactDeliveryNonceStore nonceStore,
        IOptions<ArtifactDeliverySecurityOptions> options)
    {
        _sourceProvider = sourceProvider ?? throw new ArgumentNullException(nameof(sourceProvider));
        _secretResolver = secretResolver ?? throw new ArgumentNullException(nameof(secretResolver));
        _signatureService = signatureService ?? throw new ArgumentNullException(nameof(signatureService));
        _nonceStore = nonceStore ?? throw new ArgumentNullException(nameof(nonceStore));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<RuntimeArtifactDeliveryEndpointAuthenticationResult> AuthenticateAsync(
        HttpRequest request,
        string body,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var values = ReadSignatureValues(request);
        ControlPlaneDistributionSource source;
        try
        {
            source = _sourceProvider.GetByClientId(values.KeyId);
        }
        catch (KeyNotFoundException ex)
        {
            return RuntimeArtifactDeliveryEndpointAuthenticationResult.Failure(ex.Message);
        }

        var secret = await _secretResolver.ResolveSecret(source.SecretReference, cancellationToken);
        var validation = _signatureService.Validate(
            request.Method,
            $"{request.PathBase}{request.Path}{request.QueryString}",
            body,
            values,
            source.ClientId,
            secret,
            DateTimeOffset.UtcNow,
            _options.AllowedClockSkew);

        if (!validation.Succeeded)
        {
            return RuntimeArtifactDeliveryEndpointAuthenticationResult.Failure(validation.Message);
        }

        var storedNonce = await _nonceStore.TryStore(
            values.KeyId,
            values.Nonce,
            DateTimeOffset.UtcNow.Add(_options.AllowedClockSkew),
            cancellationToken);

        return storedNonce
            ? RuntimeArtifactDeliveryEndpointAuthenticationResult.Success(source.Key)
            : RuntimeArtifactDeliveryEndpointAuthenticationResult.Failure("Signature nonce was already used.");
    }

    private static ArtifactDeliverySignatureValues ReadSignatureValues(HttpRequest request)
        => new(
            ReadHeader(request, ArtifactDeliverySecurityHeaders.KeyId),
            ReadHeader(request, ArtifactDeliverySecurityHeaders.Timestamp),
            ReadHeader(request, ArtifactDeliverySecurityHeaders.Nonce),
            ReadHeader(request, ArtifactDeliverySecurityHeaders.BodyHash),
            ReadHeader(request, ArtifactDeliverySecurityHeaders.Signature));

    private static string ReadHeader(HttpRequest request, string name)
        => request.Headers.TryGetValue(name, out var value) ? value.ToString() : string.Empty;
}
