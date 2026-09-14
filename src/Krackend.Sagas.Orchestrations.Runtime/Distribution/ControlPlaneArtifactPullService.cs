using System.Net.Http;
using System.Text;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Default manual pull client used by runtime nodes to consume control-plane releases.
/// </summary>
public sealed class ControlPlaneArtifactPullService : IControlPlaneArtifactPullService
{
    private readonly HttpClient _httpClient;
    private readonly IControlPlaneDistributionSourceProvider _sourceProvider;
    private readonly IControlPlaneAccessTokenProvider _accessTokenProvider;
    private readonly IRuntimeArtifactDeploymentService _deploymentService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ControlPlaneArtifactPullService"/> class.
    /// </summary>
    public ControlPlaneArtifactPullService(
        HttpClient httpClient,
        IControlPlaneDistributionSourceProvider sourceProvider,
        IControlPlaneAccessTokenProvider accessTokenProvider,
        IRuntimeArtifactDeploymentService deploymentService)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _sourceProvider = sourceProvider ?? throw new ArgumentNullException(nameof(sourceProvider));
        _accessTokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
        _deploymentService = deploymentService ?? throw new ArgumentNullException(nameof(deploymentService));
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ControlPlaneDistributionSource> GetSources()
        => _sourceProvider.GetAll();

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<RuntimeArtifactDeliveryPackage>> GetPendingAsync(
        string sourceKey,
        CancellationToken cancellationToken = default)
    {
        var source = _sourceProvider.GetByKey(sourceKey);
        using var request = await CreateAuthenticatedRequest(
            HttpMethod.Get,
            BuildControlPlaneUri(source, $"distribution/runtime-nodes/{source.RemoteRuntimeNodeId}/artifacts/pending"),
            source,
            [ArtifactDeliveryScope.ReleaseRead],
            cancellationToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<RuntimeArtifactDeliveryPackage[]>(
                body,
                JsonOptions())
            ?? [];
    }

    /// <inheritdoc />
    public async Task<RuntimeArtifactDeploymentResult> ApplyAsync(
        string sourceKey,
        string releaseTargetId,
        CancellationToken cancellationToken = default)
    {
        var source = _sourceProvider.GetByKey(sourceKey);
        var package = await GetPackageAsync(source, releaseTargetId, cancellationToken);
        var deployment = await _deploymentService.DeployAsync(package, source.Key, cancellationToken);
        await AcknowledgeAsync(source, releaseTargetId, deployment, cancellationToken);
        return deployment;
    }

    private async Task<RuntimeArtifactDeliveryPackage> GetPackageAsync(
        ControlPlaneDistributionSource source,
        string releaseTargetId,
        CancellationToken cancellationToken)
    {
        using var request = await CreateAuthenticatedRequest(
            HttpMethod.Get,
            BuildControlPlaneUri(source, $"distribution/runtime-nodes/{source.RemoteRuntimeNodeId}/artifacts/{releaseTargetId}"),
            source,
            [ArtifactDeliveryScope.ArtifactRead],
            cancellationToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<RuntimeArtifactDeliveryPackage>(
                body,
                JsonOptions())
            ?? throw new InvalidOperationException("Control plane returned an empty artifact package.");
    }

    private async Task AcknowledgeAsync(
        ControlPlaneDistributionSource source,
        string releaseTargetId,
        RuntimeArtifactDeploymentResult deployment,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(new RuntimeArtifactPullAckRequest
        {
            RuntimeArtifactId = deployment.RuntimeArtifactId,
            RuntimeArtifactStatus = deployment.Status
        });
        using var request = await CreateAuthenticatedRequest(
            HttpMethod.Post,
            BuildControlPlaneUri(source, $"distribution/runtime-nodes/{source.RemoteRuntimeNodeId}/artifacts/{releaseTargetId}/ack"),
            source,
            [ArtifactDeliveryScope.ArtifactAcknowledge],
            cancellationToken);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpRequestMessage> CreateAuthenticatedRequest(
        HttpMethod method,
        Uri uri,
        ControlPlaneDistributionSource source,
        IReadOnlyCollection<ArtifactDeliveryScope> scopes,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, uri);
        await _accessTokenProvider.AttachTokenAsync(request, source, scopes, cancellationToken);
        return request;
    }

    private static Uri BuildControlPlaneUri(ControlPlaneDistributionSource source, string path)
        => new($"{source.EndpointBaseUri.TrimEnd('/')}/{path.TrimStart('/')}", UriKind.Absolute);

    private static JsonSerializerOptions JsonOptions()
        => new() { PropertyNameCaseInsensitive = true };
}
