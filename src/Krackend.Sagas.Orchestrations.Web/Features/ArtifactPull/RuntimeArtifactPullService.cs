using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Web;

public sealed class RuntimeArtifactPullService : IRuntimeArtifactPullService
{
    private readonly HttpClient _httpClient;
    private readonly IRuntimeArtifactDeploymentService _deploymentService;
    private readonly RuntimeArtifactPullOptions _options;

    public RuntimeArtifactPullService(
        HttpClient httpClient,
        IRuntimeArtifactDeploymentService deploymentService,
        IOptions<RuntimeArtifactPullOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _deploymentService = deploymentService ?? throw new ArgumentNullException(nameof(deploymentService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<RuntimeArtifactPullResult> PullPending(CancellationToken cancellationToken = default)
    {
        var messages = new List<string>();
        if (string.IsNullOrWhiteSpace(_options.DistributionBaseUri))
        {
            return Fail("Runtime pull is not configured. Set 'Runtime:ArtifactPull:DistributionBaseUri'.");
        }

        if (string.IsNullOrWhiteSpace(_options.RuntimeNodeId))
        {
            return Fail("Runtime pull is not configured. Set 'Runtime:ArtifactPull:RuntimeNodeId'.");
        }

        var packages = await _httpClient.GetFromJsonAsync<RuntimeArtifactPullPackage[]>(
            BuildPendingUri(),
            cancellationToken) ?? Array.Empty<RuntimeArtifactPullPackage>();

        var activated = 0;
        var failed = 0;

        foreach (var package in packages)
        {
            var deployment = await _deploymentService.Deploy(Map(package), cancellationToken);
            if (!deployment.Accepted)
            {
                failed++;
                messages.Add($"{package.ReleaseTargetId}: {deployment.Message}");
                continue;
            }

            activated++;
            await _httpClient.PostAsJsonAsync(
                BuildAckUri(package.ReleaseTargetId),
                new RuntimeArtifactPullAckRequest { RuntimeArtifactId = deployment.RuntimeArtifactId },
                cancellationToken);
        }

        return new RuntimeArtifactPullResult
        {
            Succeeded = failed == 0,
            Pulled = packages.Length,
            Activated = activated,
            Failed = failed,
            Messages = messages
        };

        RuntimeArtifactPullResult Fail(string message)
            => new()
            {
                Succeeded = false,
                Messages = new[] { message }
            };
    }

    private RuntimeArtifactDeploymentRequest Map(RuntimeArtifactPullPackage package)
        => new()
        {
            ArtifactId = package.ArtifactId,
            ArtifactType = package.ArtifactType,
            SchemaVersion = package.SchemaVersion,
            EnvironmentKey = package.EnvironmentKey,
            OrchestrationDefinitionId = package.OrchestrationDefinitionId,
            OrchestrationVersionId = package.OrchestrationVersionId,
            OrchestrationDefinitionKey = package.OrchestrationDefinitionKey,
            Version = package.Version,
            Checksum = package.Checksum,
            PayloadJson = package.PayloadJson,
            CorrelationId = package.CorrelationId,
            PromotedBy = package.PromotedBy,
            PromotedOnUtc = package.PromotedOnUtc
        };

    private Uri BuildPendingUri()
        => new($"{_options.DistributionBaseUri.TrimEnd('/')}/distribution/runtime-nodes/{_options.RuntimeNodeId}/artifacts/pending", UriKind.Absolute);

    private Uri BuildAckUri(string releaseTargetId)
        => new($"{_options.DistributionBaseUri.TrimEnd('/')}/distribution/runtime-nodes/{_options.RuntimeNodeId}/artifacts/{releaseTargetId}/ack", UriKind.Absolute);

    private sealed class RuntimeArtifactPullAckRequest
    {
        public string RuntimeArtifactId { get; set; }
    }
}
