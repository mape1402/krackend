using System.Net.Http;

namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Default HTTP request signer for artifact delivery traffic.
/// </summary>
public sealed class DefaultArtifactDeliveryHttpRequestSigner : IArtifactDeliveryHttpRequestSigner
{
    private readonly IArtifactDeliverySecretResolver _secretResolver;
    private readonly IArtifactDeliverySignatureService _signatureService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultArtifactDeliveryHttpRequestSigner"/> class.
    /// </summary>
    public DefaultArtifactDeliveryHttpRequestSigner(
        IArtifactDeliverySecretResolver secretResolver,
        IArtifactDeliverySignatureService signatureService)
    {
        _secretResolver = secretResolver ?? throw new ArgumentNullException(nameof(secretResolver));
        _signatureService = signatureService ?? throw new ArgumentNullException(nameof(signatureService));
    }

    /// <inheritdoc />
    public async Task SignAsync(
        HttpRequestMessage request,
        string body,
        string keyId,
        string secretReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var secret = await _secretResolver.ResolveSecret(secretReference, cancellationToken);
        var signature = _signatureService.Sign(
            request.Method.Method,
            request.RequestUri?.PathAndQuery ?? "/",
            body,
            keyId,
            secret,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));

        request.Headers.Remove(ArtifactDeliverySecurityHeaders.KeyId);
        request.Headers.Remove(ArtifactDeliverySecurityHeaders.Timestamp);
        request.Headers.Remove(ArtifactDeliverySecurityHeaders.Nonce);
        request.Headers.Remove(ArtifactDeliverySecurityHeaders.BodyHash);
        request.Headers.Remove(ArtifactDeliverySecurityHeaders.Signature);
        request.Headers.TryAddWithoutValidation(ArtifactDeliverySecurityHeaders.KeyId, signature.KeyId);
        request.Headers.TryAddWithoutValidation(ArtifactDeliverySecurityHeaders.Timestamp, signature.Timestamp);
        request.Headers.TryAddWithoutValidation(ArtifactDeliverySecurityHeaders.Nonce, signature.Nonce);
        request.Headers.TryAddWithoutValidation(ArtifactDeliverySecurityHeaders.BodyHash, signature.BodyHash);
        request.Headers.TryAddWithoutValidation(ArtifactDeliverySecurityHeaders.Signature, signature.Signature);
    }
}
