using System.Net.Http;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Default Runtime-side Design node connection service.
/// </summary>
public sealed class RuntimeDesignNodeConnectionService : IRuntimeDesignNodeConnectionService
{
    private readonly HttpClient _httpClient;
    private readonly IRuntimeDesignNodeRepository _repository;
    private readonly IConnectionSecretGenerator _secretGenerator;
    private readonly IConnectionSecretHasher _secretHasher;
    private readonly IConnectionScopeFormatter _scopeFormatter;
    private readonly ConnectionCredentialPackageSerializer _packageSerializer;
    private readonly IRuntimeDesignNodeSecretProtector _secretProtector;
    private readonly IControlPlaneAccessTokenProvider _accessTokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeDesignNodeConnectionService"/> class.
    /// </summary>
    public RuntimeDesignNodeConnectionService(
        HttpClient httpClient,
        IRuntimeDesignNodeRepository repository,
        IConnectionSecretGenerator secretGenerator,
        IConnectionSecretHasher secretHasher,
        IConnectionScopeFormatter scopeFormatter,
        ConnectionCredentialPackageSerializer packageSerializer,
        IRuntimeDesignNodeSecretProtector secretProtector,
        IControlPlaneAccessTokenProvider accessTokenProvider)
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
    public async Task<RuntimeDesignNodeCredentialPackageModel> GenerateCredentialPackageAsync(
        string designNodeId,
        string issuerBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var node = await GetNode(designNodeId, cancellationToken);
        EnsureCanGenerateCredentialPackage(node);
        var material = _secretGenerator.GenerateCredential(node.Key);
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
        await _repository.UpsertAsync(node, cancellationToken);

        var envelope = _packageSerializer.CreateEnvelope(new ConnectionCredentialPackage
        {
            Issuer = "runtime",
            Target = "design",
            Mode = node.DistributionMode.ToString(),
            BaseUrl = issuerBaseUrl?.Trim().TrimEnd('/') ?? string.Empty,
            IssuerNodeId = node.Id.ToString(),
            IssuerNodeCode = node.Key,
            TargetNodeId = "design",
            TargetNodeCode = "design",
            ClientId = material.ClientId,
            ClientSecret = material.ClientSecret,
            KeyId = material.KeyId,
            Scopes = scopes,
            GeneratedAtUtc = now
        });

        return new RuntimeDesignNodeCredentialPackageModel { Json = envelope.Json, Base64 = envelope.Base64 };
    }

    /// <inheritdoc />
    public async Task ImportCredentialPackageAsync(
        ImportRuntimeDesignNodeCredentialPackageInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var node = await GetNode(input.DesignNodeId, cancellationToken);
        EnsureCanImportCredentialPackage(node);
        var package = _packageSerializer.Parse(input.Package);
        if (!string.Equals(package.Issuer, "design", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(package.Target, "runtime", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Credential package must be issued by Design for Runtime.");
        }

        node.EndpointBaseUri = string.IsNullOrWhiteSpace(package.BaseUrl) ? node.EndpointBaseUri : package.BaseUrl.Trim().TrimEnd('/');
        node.RemoteRuntimeNodeId = string.IsNullOrWhiteSpace(package.TargetNodeId) ? node.RemoteRuntimeNodeId : package.TargetNodeId.Trim();
        node.OutboundClientId = package.ClientId;
        node.OutboundKeyId = package.KeyId;
        node.ProtectedOutboundSecret = _secretProtector.Protect(package.ClientSecret);
        node.OutboundRequestedScopes = package.Scopes;
        node.OutboundCredentialStatus = ConnectionCredentialStatus.Active;
        node.OutboundCredentialImportedAtUtc = DateTime.UtcNow;
        await _repository.UpsertAsync(node, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RuntimeDesignNodeConnectionValidationModel> ValidateConnectionAsync(
        string designNodeId,
        CancellationToken cancellationToken = default)
    {
        var node = await GetNode(designNodeId, cancellationToken);
        try
        {
            EnsureCanCheckConnection(node);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri($"{node.EndpointBaseUri.TrimEnd('/')}/distribution/runtime-nodes/{node.RemoteRuntimeNodeId}/connect/validate", UriKind.Absolute));
            await _accessTokenProvider.AttachTokenAsync(
                request,
                ToSource(node),
                [ArtifactDeliveryScope.ConnectionValidate],
                cancellationToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(body)
                    ? $"Design returned HTTP {(int)response.StatusCode}."
                    : body);
            }

            return new RuntimeDesignNodeConnectionValidationModel { Succeeded = true, Message = "Connection validated." };
        }
        catch (Exception ex)
        {
            return new RuntimeDesignNodeConnectionValidationModel { Succeeded = false, Message = ex.Message };
        }
    }

    private async Task<RuntimeDesignNode> GetNode(string designNodeId, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(designNodeId, out var parsed))
        {
            throw new InvalidOperationException("Design node id is invalid.");
        }

        return await _repository.GetByIdAsync(new Id(parsed), cancellationToken);
    }

    private static IReadOnlyCollection<ArtifactDeliveryScope> GetInboundScopes(DistributionConnectionMode mode)
        => mode is DistributionConnectionMode.DesignPublishesToRuntime or DistributionConnectionMode.HybridSync
            ? [ArtifactDeliveryScope.ArtifactPush, ArtifactDeliveryScope.ConnectionValidate]
            : [ArtifactDeliveryScope.ConnectionValidate];

    private static void EnsureCanGenerateCredentialPackage(RuntimeDesignNode node)
    {
        if (node.DistributionMode is not (DistributionConnectionMode.DesignPublishesToRuntime or DistributionConnectionMode.HybridSync))
        {
            throw new InvalidOperationException("Runtime credentials are only required when Design can call Runtime.");
        }
    }

    private static void EnsureCanImportCredentialPackage(RuntimeDesignNode node)
    {
        if (node.DistributionMode is not (DistributionConnectionMode.RuntimeFetchesFromDesign or DistributionConnectionMode.HybridSync))
        {
            throw new InvalidOperationException("Design credentials are only required when Runtime can call Design.");
        }
    }

    private static void EnsureCanCheckConnection(RuntimeDesignNode node)
    {
        if (node.DistributionMode is not (DistributionConnectionMode.RuntimeFetchesFromDesign or DistributionConnectionMode.HybridSync))
        {
            throw new InvalidOperationException("Connection check only applies when Runtime can call Design.");
        }

        if (node.Status != RuntimeDesignNodeStatus.Enabled || !node.IsEnabled)
        {
            throw new InvalidOperationException("Design node must be enabled before checking the connection.");
        }

        if (node.OutboundCredentialStatus != ConnectionCredentialStatus.Active)
        {
            throw new InvalidOperationException("Design credentials are required before checking the connection.");
        }

        if (string.IsNullOrWhiteSpace(node.EndpointBaseUri))
        {
            throw new InvalidOperationException("Design endpoint is required before checking the connection.");
        }

        if (string.IsNullOrWhiteSpace(node.RemoteRuntimeNodeId))
        {
            throw new InvalidOperationException("Remote runtime node id is required before checking the connection.");
        }
    }

    private static ControlPlaneDistributionSource ToSource(RuntimeDesignNode node)
        => new()
        {
            Key = node.Key,
            Name = node.Name,
            EndpointBaseUri = node.EndpointBaseUri,
            RemoteRuntimeNodeId = node.RemoteRuntimeNodeId,
            ClientId = node.OutboundClientId,
            ProtectedSecret = node.ProtectedOutboundSecret,
            KeyId = node.OutboundKeyId,
            RequestedScopes = node.OutboundRequestedScopes,
            TokenRefreshSkewSeconds = node.TokenRefreshSkewSeconds,
            IsEnabled = node.IsEnabled
        };
}
