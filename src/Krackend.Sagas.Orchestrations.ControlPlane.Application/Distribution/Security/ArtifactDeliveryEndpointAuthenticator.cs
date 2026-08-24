using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Default control-plane authenticator for runtime artifact pull requests.
/// </summary>
public sealed class ArtifactDeliveryEndpointAuthenticator : IArtifactDeliveryEndpointAuthenticator
{
    private readonly IRuntimeNodeRepository _runtimeNodeRepository;
    private readonly IArtifactDeliverySecretResolver _secretResolver;
    private readonly IArtifactDeliverySignatureService _signatureService;
    private readonly IArtifactDeliveryNonceStore _nonceStore;
    private readonly ArtifactDeliverySecurityOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactDeliveryEndpointAuthenticator"/> class.
    /// </summary>
    public ArtifactDeliveryEndpointAuthenticator(
        IRuntimeNodeRepository runtimeNodeRepository,
        IArtifactDeliverySecretResolver secretResolver,
        IArtifactDeliverySignatureService signatureService,
        IArtifactDeliveryNonceStore nonceStore,
        IOptions<ArtifactDeliverySecurityOptions> options)
    {
        _runtimeNodeRepository = runtimeNodeRepository ?? throw new ArgumentNullException(nameof(runtimeNodeRepository));
        _secretResolver = secretResolver ?? throw new ArgumentNullException(nameof(secretResolver));
        _signatureService = signatureService ?? throw new ArgumentNullException(nameof(signatureService));
        _nonceStore = nonceStore ?? throw new ArgumentNullException(nameof(nonceStore));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<ArtifactDeliveryEndpointAuthenticationResult> AuthenticateRuntimeNodeAsync(
        HttpRequest request,
        string runtimeNodeId,
        string body,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var node = await _runtimeNodeRepository.GetById(ParseId(runtimeNodeId), cancellationToken);
        if (!node.IsEnabled || node.Status != RuntimeNodeStatus.Active)
        {
            return ArtifactDeliveryEndpointAuthenticationResult.Failure("Runtime node is not active.");
        }

        if (node.AuthenticationMode == RuntimeAuthenticationMode.None)
        {
            return ArtifactDeliveryEndpointAuthenticationResult.Failure("Runtime node does not have artifact delivery authentication configured.");
        }

        var expectedKeyId = ResolveKeyId(node.AuthenticationMode, node.ClientId, node.ApiKeyReference);
        var secretReference = ResolveSecretReference(node.AuthenticationMode, node.SecretReference, node.ApiKeyReference);
        var secret = await _secretResolver.ResolveSecret(secretReference, cancellationToken);
        var values = ReadSignatureValues(request);
        var validation = _signatureService.Validate(
            request.Method,
            $"{request.PathBase}{request.Path}{request.QueryString}",
            body,
            values,
            expectedKeyId,
            secret,
            DateTimeOffset.UtcNow,
            _options.AllowedClockSkew);

        if (!validation.Succeeded)
        {
            return ArtifactDeliveryEndpointAuthenticationResult.Failure(validation.Message);
        }

        var storedNonce = await _nonceStore.TryStore(
            values.KeyId,
            values.Nonce,
            DateTimeOffset.UtcNow.Add(_options.AllowedClockSkew),
            cancellationToken);

        return storedNonce
            ? ArtifactDeliveryEndpointAuthenticationResult.Success()
            : ArtifactDeliveryEndpointAuthenticationResult.Failure("Signature nonce was already used.");
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

    private static string ResolveKeyId(RuntimeAuthenticationMode mode, string clientId, string apiKeyReference)
        => mode == RuntimeAuthenticationMode.ApiKey && string.IsNullOrWhiteSpace(clientId)
            ? apiKeyReference
            : clientId;

    private static string ResolveSecretReference(RuntimeAuthenticationMode mode, string secretReference, string apiKeyReference)
        => mode == RuntimeAuthenticationMode.ApiKey
            ? apiKeyReference
            : secretReference;

    private static Id ParseId(string value) => new(Ulid.Parse(value));
}
