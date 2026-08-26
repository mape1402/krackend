using System.Net.Http;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Default Design-side Runtime node connection service.
/// </summary>
public sealed class RuntimeNodeConnectionApplicationService : IRuntimeNodeConnectionApplicationService
{
    private readonly HttpClient _httpClient;
    private readonly IRuntimeNodeRepository _repository;
    private readonly IConnectionSecretGenerator _secretGenerator;
    private readonly IConnectionSecretHasher _secretHasher;
    private readonly IConnectionScopeFormatter _scopeFormatter;
    private readonly ConnectionCredentialPackageSerializer _packageSerializer;
    private readonly IControlPlaneRuntimeNodeSecretProtector _secretProtector;
    private readonly IRuntimeAccessTokenProvider _accessTokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeNodeConnectionApplicationService"/> class.
    /// </summary>
    public RuntimeNodeConnectionApplicationService(
        HttpClient httpClient,
        IRuntimeNodeRepository repository,
        IConnectionSecretGenerator secretGenerator,
        IConnectionSecretHasher secretHasher,
        IConnectionScopeFormatter scopeFormatter,
        ConnectionCredentialPackageSerializer packageSerializer,
        IControlPlaneRuntimeNodeSecretProtector secretProtector,
        IRuntimeAccessTokenProvider accessTokenProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _secretGenerator = secretGenerator ?? throw new ArgumentNullException(nameof(secretGenerator));
        _secretHasher = secretHasher ?? throw new ArgumentNullException(nameof(secretHasher));
        _scopeFormatter = scopeFormatter ?? throw new ArgumentNullException(nameof(scopeFormatter));
        _packageSerializer = packageSerializer ?? throw new ArgumentNullException(nameof(packageSerializer));
        _secretProtector = secretProtector ?? throw new ArgumentNullException(nameof(secretProtector));
        _accessTokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
    }

    /// <inheritdoc />
    public async Task<RuntimeNodeCredentialPackageModel> GenerateCredentialPackage(
        string runtimeNodeId,
        string issuerBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var node = await GetNode(runtimeNodeId, cancellationToken);
        var material = _secretGenerator.GenerateCredential(node.Code);
        var now = DateTime.UtcNow;
        var scopes = _scopeFormatter.FormatMany(GetInboundScopes(node.DistributionMode));

        node.InboundClientId = material.ClientId;
        node.InboundKeyId = material.KeyId;
        node.InboundSecretHash = _secretHasher.HashSecret(material.ClientSecret);
        node.InboundAllowedScopes = scopes;
        node.InboundCredentialStatus = ConnectionCredentialStatus.Active;
        node.InboundCredentialCreatedAtUtc ??= now;
        node.InboundCredentialRotatedAtUtc = now;
        node.InboundCredentialRevokedAtUtc = null;
        await _repository.Update(node, cancellationToken);

        var envelope = _packageSerializer.CreateEnvelope(new ConnectionCredentialPackage
        {
            Issuer = "design",
            Target = "runtime",
            Mode = node.DistributionMode.ToString(),
            BaseUrl = issuerBaseUrl?.Trim().TrimEnd('/') ?? string.Empty,
            IssuerNodeCode = "design",
            TargetNodeCode = node.Code,
            ClientId = material.ClientId,
            ClientSecret = material.ClientSecret,
            KeyId = material.KeyId,
            Scopes = scopes,
            GeneratedAtUtc = now
        });

        return new RuntimeNodeCredentialPackageModel { Json = envelope.Json, Base64 = envelope.Base64 };
    }

    /// <inheritdoc />
    public async Task ImportCredentialPackage(
        ImportRuntimeNodeCredentialPackageInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var node = await GetNode(input.RuntimeNodeId, cancellationToken);
        var package = _packageSerializer.Parse(input.Package);
        if (!string.Equals(package.Issuer, "runtime", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(package.Target, "design", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Credential package must be issued by Runtime for Design.");
        }

        node.EndpointBaseUri = string.IsNullOrWhiteSpace(package.BaseUrl) ? node.EndpointBaseUri : package.BaseUrl.Trim().TrimEnd('/');
        node.OutboundClientId = package.ClientId;
        node.OutboundKeyId = package.KeyId;
        node.ProtectedOutboundSecret = _secretProtector.Protect(package.ClientSecret);
        node.OutboundRequestedScopes = package.Scopes;
        node.OutboundCredentialStatus = ConnectionCredentialStatus.Active;
        node.OutboundCredentialImportedAtUtc = DateTime.UtcNow;
        await _repository.Update(node, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RuntimeNodeConnectionValidationModel> ValidateConnection(
        string runtimeNodeId,
        CancellationToken cancellationToken = default)
    {
        var node = await GetNode(runtimeNodeId, cancellationToken);
        try
        {
            EnsureCanCheckConnection(node);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri($"{node.EndpointBaseUri.TrimEnd('/')}/runtime/distribution/connect/validate", UriKind.Absolute));
            await _accessTokenProvider.AttachTokenAsync(
                request,
                node,
                [ArtifactDeliveryScope.ConnectionValidate],
                cancellationToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(body)
                    ? $"Runtime returned HTTP {(int)response.StatusCode}."
                    : body);
            }

            return new RuntimeNodeConnectionValidationModel { Succeeded = true, Message = "Connection validated." };
        }
        catch (Exception ex)
        {
            return new RuntimeNodeConnectionValidationModel { Succeeded = false, Message = ex.Message };
        }
    }

    private async Task<RuntimeNode> GetNode(string runtimeNodeId, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(runtimeNodeId, out var parsed))
        {
            throw new InvalidOperationException("Runtime node id is invalid.");
        }

        return await _repository.GetById(new Id(parsed), cancellationToken);
    }

    private static IReadOnlyCollection<ArtifactDeliveryScope> GetInboundScopes(DistributionMode mode)
        => mode is DistributionMode.RuntimeFetchesFromDesign or DistributionMode.HybridSync
            ? [ArtifactDeliveryScope.ReleaseRead, ArtifactDeliveryScope.ArtifactRead, ArtifactDeliveryScope.ArtifactAcknowledge, ArtifactDeliveryScope.ConnectionValidate]
            : [ArtifactDeliveryScope.ConnectionValidate];

    private static void EnsureCanCheckConnection(RuntimeNode node)
    {
        if (node.DistributionMode is not (DistributionMode.DesignPublishesToRuntime or DistributionMode.HybridSync))
        {
            throw new InvalidOperationException("Connection check only applies when Design can call Runtime.");
        }

        if (node.IsDeleted)
        {
            throw new InvalidOperationException("Runtime node was deleted.");
        }

        if (node.Status != RuntimeNodeStatus.Enabled)
        {
            throw new InvalidOperationException("Runtime node must be enabled before checking the connection.");
        }

        if (node.OutboundCredentialStatus != ConnectionCredentialStatus.Active)
        {
            throw new InvalidOperationException("Runtime credentials are required before checking the connection.");
        }

        if (string.IsNullOrWhiteSpace(node.EndpointBaseUri))
        {
            throw new InvalidOperationException("Runtime endpoint is required before checking the connection.");
        }
    }
}
