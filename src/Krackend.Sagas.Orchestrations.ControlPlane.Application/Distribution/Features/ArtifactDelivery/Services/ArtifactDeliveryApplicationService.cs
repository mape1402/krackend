using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public sealed class ArtifactDeliveryApplicationService : IArtifactDeliveryApplicationService
{
    private const string DefaultRuntimeDeployPath = "runtime/artifacts/deploy";

    private readonly HttpClient _httpClient;
    private readonly IReleaseTargetRepository _releaseTargetRepository;
    private readonly IReleaseRepository _releaseRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IRuntimeNodeRepository _runtimeNodeRepository;
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly IRuntimeAccessTokenProvider _accessTokenProvider;

    public ArtifactDeliveryApplicationService(
        HttpClient httpClient,
        IReleaseTargetRepository releaseTargetRepository,
        IReleaseRepository releaseRepository,
        IArtifactRepository artifactRepository,
        IRuntimeNodeRepository runtimeNodeRepository,
        IEnvironmentRepository environmentRepository,
        IRuntimeAccessTokenProvider accessTokenProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _releaseTargetRepository = releaseTargetRepository ?? throw new ArgumentNullException(nameof(releaseTargetRepository));
        _releaseRepository = releaseRepository ?? throw new ArgumentNullException(nameof(releaseRepository));
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
        _runtimeNodeRepository = runtimeNodeRepository ?? throw new ArgumentNullException(nameof(runtimeNodeRepository));
        _environmentRepository = environmentRepository ?? throw new ArgumentNullException(nameof(environmentRepository));
        _accessTokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
    }

    public async Task<RuntimeArtifactDeliveryResult> Push(
        string releaseTargetId,
        string initiatedBy = "distribution",
        CancellationToken cancellationToken = default)
    {
        var startedAtUtc = DateTime.UtcNow;
        var target = await _releaseTargetRepository.GetById(ParseId(releaseTargetId), cancellationToken);
        var node = await _runtimeNodeRepository.GetById(target.RuntimeNodeId, cancellationToken);
        var artifact = await _artifactRepository.GetById(target.ArtifactId, cancellationToken);

        try
        {
            EnsureRuntimeNodeCanDistribute(node);

            if (target.Status == ReleaseTargetStatus.Activated
                && target.ActivationStatus == ActivationStatus.Activated
                && !string.IsNullOrWhiteSpace(target.RuntimeVersionApplied))
            {
                await MarkPublished(target, cancellationToken);
                return CreateResult(
                    target,
                    true,
                    "Activated",
                    "Runtime artifact was already promoted and activated.",
                    target.RuntimeVersionApplied);
            }

            if (node.DistributionMode == DistributionMode.RuntimeFetchesFromDesign)
            {
                target.Status = ReleaseTargetStatus.AvailableForPull;
                target.AvailableAtUtc ??= DateTime.UtcNow;
                await _releaseTargetRepository.Update(target, cancellationToken);
                return CreateResult(target, true, "AvailableForPull", "Runtime node is configured for pull.");
            }

            if (node.OutboundCredentialStatus != ConnectionCredentialStatus.Active)
            {
                throw new InvalidOperationException($"Runtime node '{node.Name}' outbound credential is not active.");
            }

            if (string.IsNullOrWhiteSpace(node.EndpointBaseUri))
            {
                throw new InvalidOperationException($"Runtime node '{node.Name}' does not have an endpoint base URI.");
            }

            target.Status = ReleaseTargetStatus.InProgress;
            await _releaseTargetRepository.Update(target, cancellationToken);

            var package = await BuildPackage(target, node, artifact, cancellationToken);
            using var requestMessage = await CreateAuthenticatedDeployRequest(node, package, cancellationToken);
            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            var bodyText = await response.Content.ReadAsStringAsync(cancellationToken);
            var body = TryReadDeploymentResponse(bodyText);

            if (!response.IsSuccessStatusCode || body?.Accepted != true)
            {
                throw new InvalidOperationException(
                    body?.Message
                    ?? ExtractRuntimeError(bodyText)
                    ?? $"Runtime returned HTTP {(int)response.StatusCode}.");
            }

            target.DeliveredAtUtc = DateTime.UtcNow;
            target.AcknowledgedAtUtc = target.DeliveredAtUtc;
            target.RuntimeVersionApplied = body.RuntimeArtifactId;
            target.FailureReason = string.Empty;
            await AddAttempt(target.Id, "Push", initiatedBy, startedAtUtc, true, body.RuntimeArtifactId, null, cancellationToken);

            if (IsRuntimeReady(body.Status))
            {
                target.Status = ReleaseTargetStatus.Activated;
                target.ActivationStatus = ActivationStatus.Activated;
                target.ActivatedAtUtc = target.DeliveredAtUtc;
                await _releaseTargetRepository.Update(target, cancellationToken);
                await MarkPublished(target, cancellationToken);

                return CreateResult(target, true, "Activated", "Runtime artifact pushed and activated.", body.RuntimeArtifactId);
            }

            target.Status = ReleaseTargetStatus.Delivered;
            target.ActivationStatus = ActivationStatus.Activating;
            target.ActivatedAtUtc = null;
            await _releaseTargetRepository.Update(target, cancellationToken);

            return CreateResult(target, true, "Delivered", "Runtime artifact pushed and accepted for activation.", body.RuntimeArtifactId);
        }
        catch (Exception ex)
        {
            target.Status = node.DistributionMode == DistributionMode.HybridSync
                ? ReleaseTargetStatus.AvailableForPull
                : ReleaseTargetStatus.Failed;
            target.ActivationStatus = ActivationStatus.ActivationFailed;
            target.AvailableAtUtc = node.DistributionMode == DistributionMode.HybridSync ? DateTime.UtcNow : target.AvailableAtUtc;
            target.FailedAtUtc = DateTime.UtcNow;
            target.FailureReason = ex.Message;
            await _releaseTargetRepository.Update(target, cancellationToken);
            if (target.Status == ReleaseTargetStatus.Failed)
            {
                await MarkReleaseTargetFailed(target, cancellationToken);
            }

            await AddAttempt(target.Id, "Push", initiatedBy, startedAtUtc, false, null, ex.Message, cancellationToken);

            return CreateResult(target, false, target.Status.ToString(), ex.Message);
        }
    }

    public async Task<IReadOnlyCollection<RuntimeArtifactDeliveryPackage>> GetPendingForPull(
        string runtimeNodeId,
        CancellationToken cancellationToken = default)
    {
        var node = await _runtimeNodeRepository.GetById(ParseId(runtimeNodeId), cancellationToken);
        EnsureRuntimeNodeCanDistribute(node);
        var targets = await _releaseTargetRepository.GetPendingForRuntimeNode(node.Id, cancellationToken);
        var packages = new List<RuntimeArtifactDeliveryPackage>();

        foreach (var target in targets)
        {
            if (target.Status == ReleaseTargetStatus.Pending)
            {
                target.Status = ReleaseTargetStatus.AvailableForPull;
                target.AvailableAtUtc = DateTime.UtcNow;
                await _releaseTargetRepository.Update(target, cancellationToken);
            }

            var artifact = await _artifactRepository.GetById(target.ArtifactId, cancellationToken);
            packages.Add(await BuildPackage(target, node, artifact, cancellationToken));
        }

        return packages;
    }

    public async Task<RuntimeArtifactDeliveryPackage> GetForPull(
        string runtimeNodeId,
        string releaseTargetId,
        CancellationToken cancellationToken = default)
    {
        var node = await _runtimeNodeRepository.GetById(ParseId(runtimeNodeId), cancellationToken);
        EnsureRuntimeNodeCanDistribute(node);
        var target = await _releaseTargetRepository.GetById(ParseId(releaseTargetId), cancellationToken);
        if (target.RuntimeNodeId != node.Id)
        {
            throw new InvalidOperationException("Release target does not belong to the runtime node.");
        }

        if (target.Status is not (ReleaseTargetStatus.AvailableForPull or ReleaseTargetStatus.Pending))
        {
            throw new InvalidOperationException("Release target is not available for pull.");
        }

        if (target.Status == ReleaseTargetStatus.Pending)
        {
            target.Status = ReleaseTargetStatus.AvailableForPull;
            target.AvailableAtUtc = DateTime.UtcNow;
            await _releaseTargetRepository.Update(target, cancellationToken);
        }

        var artifact = await _artifactRepository.GetById(target.ArtifactId, cancellationToken);
        return await BuildPackage(target, node, artifact, cancellationToken);
    }

    public async Task<RuntimeArtifactDeliveryResult> AcknowledgePull(
        string runtimeNodeId,
        string releaseTargetId,
        string runtimeArtifactId,
        string runtimeArtifactStatus,
        CancellationToken cancellationToken = default)
    {
        var nodeId = ParseId(runtimeNodeId);
        var target = await _releaseTargetRepository.GetById(ParseId(releaseTargetId), cancellationToken);
        if (target.RuntimeNodeId != nodeId)
        {
            throw new InvalidOperationException("Release target does not belong to the runtime node.");
        }

        var ready = IsRuntimeReady(runtimeArtifactStatus);
        target.Status = ready ? ReleaseTargetStatus.Activated : ReleaseTargetStatus.Acknowledged;
        target.ActivationStatus = ready ? ActivationStatus.Activated : ActivationStatus.Activating;
        target.DeliveredAtUtc ??= DateTime.UtcNow;
        target.AcknowledgedAtUtc = DateTime.UtcNow;
        target.ActivatedAtUtc = ready ? target.AcknowledgedAtUtc : null;
        target.RuntimeVersionApplied = runtimeArtifactId;
        target.FailureReason = string.Empty;
        await _releaseTargetRepository.Update(target, cancellationToken);
        if (ready)
        {
            await MarkPublished(target, cancellationToken);
        }

        await AddAttempt(target.Id, "Pull", "runtime", DateTime.UtcNow, true, runtimeArtifactId, null, cancellationToken);

        return ready
            ? CreateResult(target, true, "Activated", "Runtime pull acknowledged and activated.", runtimeArtifactId)
            : CreateResult(target, true, "Acknowledged", "Runtime pull acknowledged and accepted for activation.", runtimeArtifactId);
    }

    private async Task<RuntimeArtifactDeliveryPackage> BuildPackage(
        ReleaseTarget target,
        RuntimeNode node,
        Artifact artifact,
        CancellationToken cancellationToken)
    {
        var environment = await _environmentRepository.GetById(node.EnvironmentId, cancellationToken);
        return new RuntimeArtifactDeliveryPackage
        {
            ReleaseTargetId = target.Id.ToString(),
            ArtifactId = artifact.Id.ToString(),
            ArtifactType = artifact.ArtifactType,
            SchemaVersion = artifact.SchemaVersion,
            EnvironmentKey = environment.Code,
            OrchestrationDefinitionId = artifact.OrchestrationDefinitionId,
            OrchestrationVersionId = artifact.OrchestrationVersionId,
            OrchestrationDefinitionKey = ExtractOrchestrationDefinitionKey(artifact.Payload, artifact.OrchestrationDefinitionId),
            Version = artifact.VersionNumber,
            Checksum = artifact.Checksum,
            PayloadJson = artifact.Payload,
            CorrelationId = target.CorrelationId,
            PromotedBy = "distribution",
            PromotedOnUtc = DateTime.UtcNow
        };
    }

    private async Task<HttpRequestMessage> CreateAuthenticatedDeployRequest(
        RuntimeNode node,
        RuntimeArtifactDeliveryPackage package,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(package);
        var request = new HttpRequestMessage(HttpMethod.Post, BuildDeployUri(node))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        await _accessTokenProvider.AttachTokenAsync(
            request,
            node,
            [ArtifactDeliveryScope.ArtifactPush],
            cancellationToken);

        return request;
    }

    private async Task AddAttempt(
        Id targetId,
        string action,
        string initiatedBy,
        DateTime startedAtUtc,
        bool succeeded,
        string externalReference,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        await _releaseTargetRepository.AddAttempt(new ReleaseAttempt
        {
            Id = Id.New(),
            ReleaseTargetId = targetId,
            Action = action,
            InitiatedBy = initiatedBy,
            StartedAtUtc = startedAtUtc,
            FinishedAtUtc = DateTime.UtcNow,
            Succeeded = succeeded,
            ErrorCode = succeeded ? string.Empty : "ArtifactDeliveryFailed",
            ErrorMessage = errorMessage ?? string.Empty,
            ExternalReference = externalReference ?? string.Empty
        }, cancellationToken);
    }

    private async Task MarkPublished(ReleaseTarget target, CancellationToken cancellationToken)
    {
        await _artifactRepository.SetPublished(target.ArtifactId, true, cancellationToken);

        if (target.ReleaseId.HasValue)
        {
            await _releaseRepository.ApplyTargetStatus(
                target.ReleaseId.Value,
                target.RuntimeNodeId,
                ReleaseStatus.Completed,
                cancellationToken);
        }
    }

    private async Task MarkReleaseTargetFailed(ReleaseTarget target, CancellationToken cancellationToken)
    {
        if (target.ReleaseId.HasValue)
        {
            await _releaseRepository.ApplyTargetStatus(
                target.ReleaseId.Value,
                target.RuntimeNodeId,
                ReleaseStatus.Failed,
                cancellationToken);
        }
    }

    private static Uri BuildDeployUri(RuntimeNode node)
    {
        var baseUri = node.EndpointBaseUri.TrimEnd('/');
        var path = string.IsNullOrWhiteSpace(node.EndpointApiPath)
            ? DefaultRuntimeDeployPath
            : node.EndpointApiPath.Trim('/');

        return new Uri($"{baseUri}/{path}", UriKind.Absolute);
    }

    private static string ExtractOrchestrationDefinitionKey(string payload, string fallback)
    {
        var json = JsonNode.Parse(payload);
        return json?["Key"]?.GetValue<string>()
            ?? json?["key"]?.GetValue<string>()
            ?? json?["orchestrationDefinitionKey"]?.GetValue<string>()
            ?? json?["OrchestrationDefinitionKey"]?.GetValue<string>()
            ?? json?["orchestrationDefinitionId"]?.GetValue<string>()
            ?? json?["OrchestrationDefinitionId"]?.GetValue<string>()
            ?? fallback
            ?? throw new InvalidOperationException("Artifact payload does not contain orchestration definition key.");
    }

    private static Id ParseId(string value) => new(Ulid.Parse(value));

    private static void EnsureRuntimeNodeCanDistribute(RuntimeNode node)
    {
        if (node.IsDeleted)
        {
            throw new InvalidOperationException($"Runtime node '{node.Name}' was deleted.");
        }

        if (node.Status != RuntimeNodeStatus.Enabled)
        {
            throw new InvalidOperationException($"Runtime node '{node.Name}' is not enabled.");
        }
    }

    private static RuntimeArtifactDeliveryResult CreateResult(
        ReleaseTarget target,
        bool succeeded,
        string status,
        string message,
        string externalReference = null)
        => new()
        {
            Succeeded = succeeded,
            ReleaseTargetId = target.Id.ToString(),
            RuntimeNodeId = target.RuntimeNodeId.ToString(),
            ArtifactId = target.ArtifactId.ToString(),
            Status = status,
            Message = message,
            ExternalReference = externalReference ?? string.Empty
        };

    private static RuntimeArtifactDeploymentResult TryReadDeploymentResponse(string bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText))
        {
            return null;
        }

        var trimmed = bodyText.TrimStart();
        if (!trimmed.StartsWith('{'))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RuntimeArtifactDeploymentResult>(
                bodyText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ExtractRuntimeError(string bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText))
        {
            return null;
        }

        var firstLine = bodyText
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
            ?.Trim();

        return string.IsNullOrWhiteSpace(firstLine)
            ? null
            : firstLine.Length > 500 ? firstLine[..500] : firstLine;
    }

    private static bool IsRuntimeReady(string status)
        => string.Equals(status, "Ready", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Activated", StringComparison.OrdinalIgnoreCase);
}
