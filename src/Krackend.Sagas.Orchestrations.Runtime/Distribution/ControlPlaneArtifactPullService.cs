using System.Net.Http;
using System.Text;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

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
    private readonly IRuntimeArtifactRepository _artifactRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ControlPlaneArtifactPullService"/> class.
    /// </summary>
    public ControlPlaneArtifactPullService(
        HttpClient httpClient,
        IControlPlaneDistributionSourceProvider sourceProvider,
        IControlPlaneAccessTokenProvider accessTokenProvider,
        IRuntimeArtifactDeploymentService deploymentService,
        IRuntimeArtifactRepository artifactRepository = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _sourceProvider = sourceProvider ?? throw new ArgumentNullException(nameof(sourceProvider));
        _accessTokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
        _deploymentService = deploymentService ?? throw new ArgumentNullException(nameof(deploymentService));
        _artifactRepository = artifactRepository;
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
    public async Task<PendingArtifactPage> GetPendingPageAsync(string sourceKey, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var source = _sourceProvider.GetByKey(sourceKey);
        var page = Math.Max(1, pageNumber);
        var size = Math.Clamp(pageSize, 1, 100);
        using var request = await CreateAuthenticatedRequest(HttpMethod.Get,
            BuildControlPlaneUri(source, $"distribution/runtime-nodes/{source.RemoteRuntimeNodeId}/artifacts/pending/page?page={page}&pageSize={size}"),
            source, [ArtifactDeliveryScope.ReleaseRead], cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<PendingArtifactPage>(await response.Content.ReadAsStringAsync(cancellationToken), JsonOptions())
            ?? throw new InvalidOperationException("Control plane returned an empty pending release page.");
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
        if (deployment.Accepted)
        {
            try
            {
                await AcknowledgeAsync(source, releaseTargetId, deployment, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new ArtifactPullAcknowledgementException(deployment, exception);
            }
        }
        return deployment;
    }

    /// <inheritdoc />
    public async Task<RuntimeArtifactDeploymentResult> RetryAcknowledgementAsync(
        string sourceKey, string releaseTargetId, string runtimeArtifactId, CancellationToken cancellationToken = default)
    {
        if (_artifactRepository is null)
            throw new InvalidOperationException("Runtime artifact storage is required for acknowledgement recovery.");
        if (!Ulid.TryParse(runtimeArtifactId, out var id))
            throw new ArgumentException("Invalid runtime artifact identifier.", nameof(runtimeArtifactId));

        var source = _sourceProvider.GetByKey(sourceKey);
        var package = await GetPackageAsync(source, releaseTargetId, cancellationToken);
        var artifact = await _artifactRepository.GetById(new Id(id), cancellationToken);
        if (artifact is null || string.IsNullOrWhiteSpace(package.ReleaseId) || string.IsNullOrWhiteSpace(package.ArtifactId) ||
            !string.Equals(artifact.OrchestrationDefinitionKey, package.OrchestrationDefinitionKey, StringComparison.Ordinal) ||
            !string.Equals(artifact.Version.ToString(), package.Version, StringComparison.Ordinal) ||
            !string.Equals(artifact.LastReleaseId, package.ReleaseId, StringComparison.Ordinal) ||
            !string.Equals(artifact.LastControlPlaneArtifactId, package.ArtifactId, StringComparison.Ordinal))
            throw new InvalidOperationException("The release does not match the installed runtime artifact.");
        if (artifact.Status == RuntimeOrchestrationArtifactStatus.Failed)
            throw new InvalidOperationException("The runtime artifact failed projection and cannot be acknowledged as activating.");

        var result = new RuntimeArtifactDeploymentResult
        {
            Accepted = true, RuntimeArtifactId = artifact.Id.ToString(), Status = artifact.Status.ToString(),
            Message = "The runtime artifact was already installed."
        };
        await AcknowledgeAsync(source, releaseTargetId, result, cancellationToken);
        return result;
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
