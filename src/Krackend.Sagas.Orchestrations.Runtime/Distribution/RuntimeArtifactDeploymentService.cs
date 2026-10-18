using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Default runtime artifact installer.
/// </summary>
public sealed class RuntimeArtifactDeploymentService : IRuntimeArtifactDeploymentService
{
    private readonly IRuntimeArtifactRepository _artifactRepository;
    private readonly IRuntimeStorageUnitOfWork _unitOfWork;
    private readonly IRuntimeArtifactProjectionScheduler _projectionScheduler;
    private readonly IRuntimeArtifactCompatibilityValidator _compatibilityValidator;
    private readonly IRuntimeIngressConfigurationRepository _ingressConfigurationRepository;
    private readonly IRuntimeArtifactSerializer _artifactSerializer;
    private readonly IOrchestrationInstanceRepository _instanceRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeArtifactDeploymentService"/> class.
    /// </summary>
    public RuntimeArtifactDeploymentService(
        IRuntimeArtifactRepository artifactRepository,
        IRuntimeStorageUnitOfWork unitOfWork,
        IRuntimeArtifactProjectionScheduler projectionScheduler,
        IRuntimeArtifactCompatibilityValidator compatibilityValidator,
        IRuntimeIngressConfigurationRepository ingressConfigurationRepository,
        IRuntimeArtifactSerializer artifactSerializer,
        IOrchestrationInstanceRepository instanceRepository)
    {
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _projectionScheduler = projectionScheduler ?? throw new ArgumentNullException(nameof(projectionScheduler));
        _compatibilityValidator = compatibilityValidator ?? throw new ArgumentNullException(nameof(compatibilityValidator));
        _ingressConfigurationRepository = ingressConfigurationRepository ?? throw new ArgumentNullException(nameof(ingressConfigurationRepository));
        _artifactSerializer = artifactSerializer ?? throw new ArgumentNullException(nameof(artifactSerializer));
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
    }

    /// <inheritdoc />
    public async Task<RuntimeArtifactDeploymentResult> DeployAsync(
        RuntimeArtifactDeliveryPackage package,
        string sourceKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (!TryValidatePackage(package, out var packageValidationError, out var payload, out var version, out var artifactId, out var sourceVersionId))
        {
            return Rejected(packageValidationError);
        }

        try
        {
            return ResolveOperation(package.ArtifactType) switch
            {
                RuntimeArtifactOperation.Deploy => await DeployNewVersionAsync(
                    package, sourceKey, payload, version, artifactId, sourceVersionId, cancellationToken),
                RuntimeArtifactOperation.Deprecate => await DeprecateAsync(
                    package, sourceKey, version, cancellationToken),
                RuntimeArtifactOperation.Archive => await ArchiveAsync(
                    package, sourceKey, version, cancellationToken),
                _ => Rejected($"Artifact type '{package.ArtifactType}' is not supported.")
            };
        }
        catch (InvalidOperationException exception)
        {
            return Rejected(exception.Message);
        }
    }

    private async Task<RuntimeArtifactDeploymentResult> DeployNewVersionAsync(
        RuntimeArtifactDeliveryPackage package,
        string sourceKey,
        JsonNode payload,
        SemanticVersion version,
        Id artifactId,
        Id sourceVersionId,
        CancellationToken cancellationToken)
    {
        var compatibility = await _compatibilityValidator.ValidateAsync(payload, cancellationToken);
        if (!compatibility.Succeeded)
        {
            return Rejected(
                $"Artifact runtime compatibility validation failed ({compatibility.ErrorCode}): {compatibility.ErrorMessage}");
        }

        var strategy = ResolveStrategy(package.ReleaseStrategy);
        var existingArtifact = await TryGetExistingArtifact(package.OrchestrationDefinitionKey, version, cancellationToken);
        if (existingArtifact is not null &&
            !string.Equals(existingArtifact.ArtifactChecksum.Value, package.Checksum, StringComparison.Ordinal))
        {
            return Rejected(
                $"Artifact '{package.OrchestrationDefinitionKey}' v{package.Version} already exists with a different checksum.");
        }

        if (existingArtifact is not null && existingArtifact.LifecycleStatus != RuntimeOrchestrationLifecycleStatus.Active)
        {
            return Rejected(
                $"Artifact '{package.OrchestrationDefinitionKey}' v{package.Version} is {existingArtifact.LifecycleStatus} and cannot be deployed again.");
        }

        if (existingArtifact is not null &&
            existingArtifact.ArtifactChecksum.Value == package.Checksum &&
            existingArtifact.Status == RuntimeOrchestrationArtifactStatus.Ready)
        {
            return Accepted(existingArtifact, RuntimeOrchestrationArtifactStatus.Ready,
                $"Artifact '{existingArtifact.OrchestrationDefinitionKey}' v{existingArtifact.Version} is already ready.");
        }

        var conflictingArtifacts = await FindTriggerConflictsAsync(payload, existingArtifact?.Id, cancellationToken);
        if (conflictingArtifacts.Count > 0 && strategy == RuntimeReleaseStrategy.Direct)
        {
            return Rejected(
                "Direct deployment would create competing trigger ingress. Use Rollout to replace the active trigger subscription.");
        }

        var now = DateTime.UtcNow;
        var ingressGeneration = existingArtifact?.ArtifactChecksum.Value == package.Checksum
            ? Math.Max(existingArtifact.IngressGeneration, 1)
            : (existingArtifact?.IngressGeneration ?? 0) + 1;
        var artifact = new RuntimeOrchestrationArtifact
        {
            Id = existingArtifact?.Id ?? artifactId,
            OrchestrationDefinitionKey = package.OrchestrationDefinitionKey,
            ArtifactType = package.ArtifactType,
            SourceOrchestrationVersionId = sourceVersionId,
            Version = version,
            ArtifactChecksum = new Checksum(package.Checksum),
            ArtifactPayload = payload,
            Status = RuntimeOrchestrationArtifactStatus.Pending,
            LifecycleStatus = RuntimeOrchestrationLifecycleStatus.Active,
            LastReleaseId = package.ReleaseId ?? string.Empty,
            LastControlPlaneArtifactId = package.ArtifactId,
            ReleaseStrategy = strategy.ToString(),
            IngressGeneration = ingressGeneration,
            IsActive = true,
            LoadedToCache = false,
            DeployedOnUtc = existingArtifact?.DeployedOnUtc ?? now,
            ActivatedOnUtc = null,
            ProjectionStartedOnUtc = null,
            ProjectionCompletedOnUtc = null,
            ProjectionFailedOnUtc = null,
            ProjectionError = null,
            RetiredOnUtc = null,
            SupersededByArtifactId = null,
            Notes = $"Installed from '{sourceKey}' release '{package.ReleaseId}' target '{package.ReleaseTargetId}'."
        };

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        using (_unitOfWork.DeferAutoSave())
        {
            await _artifactRepository.Upsert(artifact, cancellationToken);
            if (conflictingArtifacts.Count > 0)
            {
                await _ingressConfigurationRepository.DeactivateTriggersForArtifactsAsync(
                    conflictingArtifacts.Select(x => x.Id).ToArray(),
                    cancellationToken);
            }

            await ScheduleProjectionAsync(artifact, sourceKey, now, cancellationToken);
            await _unitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Accepted(artifact, RuntimeOrchestrationArtifactStatus.Pending,
            $"Artifact '{artifact.OrchestrationDefinitionKey}' v{artifact.Version} accepted for ingress projection.");
    }

    private async Task<RuntimeArtifactDeploymentResult> DeprecateAsync(
        RuntimeArtifactDeliveryPackage package,
        string sourceKey,
        SemanticVersion version,
        CancellationToken cancellationToken)
    {
        EnsureDirectLifecycleStrategy(package.ReleaseStrategy);
        var artifact = await TryGetExistingArtifact(package.OrchestrationDefinitionKey, version, cancellationToken);
        if (artifact is null)
        {
            return Rejected($"Artifact '{package.OrchestrationDefinitionKey}' v{package.Version} is not installed in this runtime.");
        }

        if (!string.Equals(artifact.ArtifactChecksum.Value, package.Checksum, StringComparison.Ordinal))
        {
            return Rejected("Deprecation artifact checksum does not match the installed orchestration version.");
        }

        if (artifact.LifecycleStatus == RuntimeOrchestrationLifecycleStatus.Archived)
        {
            return Rejected("An archived orchestration version cannot be deprecated.");
        }

        if (artifact.LifecycleStatus == RuntimeOrchestrationLifecycleStatus.Deprecated &&
            string.Equals(artifact.LastControlPlaneArtifactId, package.ArtifactId, StringComparison.Ordinal))
        {
            return Accepted(artifact, artifact.Status, "Artifact is already deprecated.");
        }

        var now = DateTime.UtcNow;
        artifact.LifecycleStatus = RuntimeOrchestrationLifecycleStatus.Deprecated;
        artifact.LastReleaseId = package.ReleaseId ?? string.Empty;
        artifact.LastControlPlaneArtifactId = package.ArtifactId;
        artifact.ReleaseStrategy = RuntimeReleaseStrategy.Direct.ToString();
        artifact.Status = RuntimeOrchestrationArtifactStatus.Pending;
        artifact.IngressGeneration = Math.Max(artifact.IngressGeneration, 1) + 1;
        artifact.ProjectionStartedOnUtc = null;
        artifact.ProjectionCompletedOnUtc = null;
        artifact.ProjectionFailedOnUtc = null;
        artifact.ProjectionError = null;
        artifact.Notes = $"Deprecated from '{sourceKey}' release '{package.ReleaseId}' target '{package.ReleaseTargetId}'.";

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        using (_unitOfWork.DeferAutoSave())
        {
            await _artifactRepository.Upsert(artifact, cancellationToken);
            await ScheduleProjectionAsync(artifact, sourceKey, now, cancellationToken);
            await _unitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Accepted(artifact, RuntimeOrchestrationArtifactStatus.Pending,
            $"Artifact '{artifact.OrchestrationDefinitionKey}' v{artifact.Version} was deprecated and will retain only its callback ingress.");
    }

    private async Task<RuntimeArtifactDeploymentResult> ArchiveAsync(
        RuntimeArtifactDeliveryPackage package,
        string sourceKey,
        SemanticVersion version,
        CancellationToken cancellationToken)
    {
        EnsureDirectLifecycleStrategy(package.ReleaseStrategy);
        var artifact = await TryGetExistingArtifact(package.OrchestrationDefinitionKey, version, cancellationToken);
        if (artifact is null)
        {
            return Rejected($"Artifact '{package.OrchestrationDefinitionKey}' v{package.Version} is not installed in this runtime.");
        }

        if (!string.Equals(artifact.ArtifactChecksum.Value, package.Checksum, StringComparison.Ordinal))
        {
            return Rejected("Archive artifact checksum does not match the installed orchestration version.");
        }

        if (artifact.LifecycleStatus == RuntimeOrchestrationLifecycleStatus.Archived &&
            string.Equals(artifact.LastControlPlaneArtifactId, package.ArtifactId, StringComparison.Ordinal))
        {
            return Accepted(artifact, RuntimeOrchestrationArtifactStatus.Retired, "Artifact is already archived.");
        }

        if (artifact.LifecycleStatus != RuntimeOrchestrationLifecycleStatus.Deprecated)
        {
            return Rejected("Only a deprecated orchestration version can be archived.");
        }

        var recoverable = await _instanceRepository.GetRecoverable(int.MaxValue, cancellationToken);
        if (recoverable.Any(x => x.RuntimeOrchestrationArtifactId == artifact.Id))
        {
            return Rejected("The orchestration version still has active SAGA instances and cannot be archived.");
        }

        artifact.LifecycleStatus = RuntimeOrchestrationLifecycleStatus.Archived;
        artifact.LastReleaseId = package.ReleaseId ?? string.Empty;
        artifact.LastControlPlaneArtifactId = package.ArtifactId;
        artifact.ReleaseStrategy = RuntimeReleaseStrategy.Direct.ToString();
        artifact.IsActive = false;
        artifact.Status = RuntimeOrchestrationArtifactStatus.Retired;
        artifact.RetiredOnUtc = DateTime.UtcNow;
        artifact.IngressGeneration = Math.Max(artifact.IngressGeneration, 1) + 1;
        artifact.Notes = $"Archived from '{sourceKey}' release '{package.ReleaseId}' target '{package.ReleaseTargetId}'.";

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        using (_unitOfWork.DeferAutoSave())
        {
            await _artifactRepository.Upsert(artifact, cancellationToken);
            await _ingressConfigurationRepository.DeactivateForArtifactsAsync([artifact.Id], cancellationToken);
            await _unitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Accepted(artifact, RuntimeOrchestrationArtifactStatus.Retired,
            $"Artifact '{artifact.OrchestrationDefinitionKey}' v{artifact.Version} was archived and its ingress was removed.");
    }

    private static bool TryValidatePackage(
        RuntimeArtifactDeliveryPackage package,
        out string error,
        out JsonNode payload,
        out SemanticVersion version,
        out Id artifactId,
        out Id sourceVersionId)
    {
        error = string.Empty;
        payload = null;
        version = default;
        artifactId = default;
        sourceVersionId = default;

        try
        {
            EnsureRequired(package.ArtifactId, nameof(package.ArtifactId));
            EnsureRequired(package.ArtifactType, nameof(package.ArtifactType));
            EnsureRequired(package.OrchestrationDefinitionKey, nameof(package.OrchestrationDefinitionKey));
            EnsureRequired(package.OrchestrationVersionId, nameof(package.OrchestrationVersionId));
            EnsureRequired(package.Version, nameof(package.Version));
            EnsureRequired(package.Checksum, nameof(package.Checksum));
            EnsureRequired(package.PayloadJson, nameof(package.PayloadJson));

            version = ParseVersion(package.Version);
            artifactId = ParseId(package.ArtifactId);
            sourceVersionId = ParseId(package.OrchestrationVersionId);
            payload = JsonNode.Parse(package.PayloadJson)
                ?? throw new InvalidOperationException("Artifact payload is empty.");
            if (payload is not JsonObject payloadObject)
            {
                throw new InvalidOperationException("Artifact payload must be a JSON object.");
            }

            var payloadKey = ReadRequiredPayloadString(payloadObject, "Key", "key");
            var payloadVersion = ReadRequiredPayloadString(payloadObject, "Version", "version");
            var payloadVersionId = ReadRequiredPayloadString(payloadObject, "OrchestrationVersionId", "orchestrationVersionId");
            var payloadChecksum = ReadRequiredPayloadString(payloadObject, "Checksum", "checksum");

            if (!string.Equals(payloadKey, package.OrchestrationDefinitionKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Artifact package key '{package.OrchestrationDefinitionKey}' does not match payload key '{payloadKey}'.");
            }

            if (!string.Equals(payloadVersion, package.Version, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Artifact package version '{package.Version}' does not match payload version '{payloadVersion}'.");
            }

            if (!string.Equals(payloadVersionId, package.OrchestrationVersionId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Artifact package orchestration version id '{package.OrchestrationVersionId}' does not match payload version id '{payloadVersionId}'.");
            }

            if (!string.Equals(payloadChecksum, package.Checksum, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Artifact package checksum '{package.Checksum}' does not match payload checksum '{payloadChecksum}'.");
            }

            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or JsonException or InvalidOperationException)
        {
            error = $"Artifact package is invalid: {exception.Message}";
            return false;
        }
    }

    private static RuntimeArtifactDeploymentResult Rejected(string message)
        => new()
        {
            Accepted = false,
            Status = "Rejected",
            Message = message
        };

    private static void EnsureRequired(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{name} is required.");
        }
    }

    private static string ReadRequiredPayloadString(
        JsonObject payload,
        string pascalName,
        string camelName)
    {
        var value = payload[camelName] ?? payload[pascalName];
        var text = ReadPayloadString(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"Payload field '{camelName}' is required.");
        }

        return text;
    }

    private static string ReadPayloadString(JsonNode value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is JsonObject valueObject)
        {
            return ReadPayloadString(valueObject["value"] ?? valueObject["Value"]);
        }

        return value.GetValueKind() == System.Text.Json.JsonValueKind.String
            ? value.GetValue<string>()
            : value.ToJsonString();
    }

    private async Task<RuntimeOrchestrationArtifact> TryGetExistingArtifact(
        string orchestrationDefinitionKey,
        SemanticVersion version,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _artifactRepository.GetByVersion(
                orchestrationDefinitionKey,
                version,
                cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private async Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> FindTriggerConflictsAsync(
        JsonNode incomingPayload,
        Id? artifactBeingUpdated,
        CancellationToken cancellationToken)
    {
        var incomingTriggers = TryGetTriggerIdentities(incomingPayload);
        if (incomingTriggers.Count == 0)
        {
            return [];
        }

        var artifacts = await _artifactRepository.GetAll(cancellationToken);
        var conflicts = new List<RuntimeOrchestrationArtifact>();
        foreach (var artifact in artifacts)
        {
            if (!artifact.IsActive ||
                artifact.LifecycleStatus != RuntimeOrchestrationLifecycleStatus.Active ||
                (artifactBeingUpdated.HasValue && artifact.Id == artifactBeingUpdated.Value))
            {
                continue;
            }

            if (TryGetTriggerIdentities(artifact.ArtifactPayload).Overlaps(incomingTriggers))
            {
                conflicts.Add(artifact);
            }
        }

        return conflicts;
    }

    private async Task ScheduleProjectionAsync(
        RuntimeOrchestrationArtifact artifact,
        string sourceKey,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await _projectionScheduler.ScheduleProjectionAsync(new RuntimeArtifactProjectionRequest
        {
            ArtifactId = artifact.Id.ToString(),
            IngressGeneration = artifact.IngressGeneration,
            RequestedBy = sourceKey,
            RequestedOnUtc = now
        }, cancellationToken);
    }

    private static HashSet<string> GetTriggerIdentities(OrchestrationArtifact artifact)
        => artifact.TriggerBindings
            .Where(x => x.IsEnabled && x.TriggerChannel is EventTriggerChannelArtifact)
            .Select(x => (EventTriggerChannelArtifact)x.TriggerChannel)
            .Select(x => $"{Normalize(x.Topic)}|{Normalize(x.Version.ToString())}")
            .ToHashSet(StringComparer.Ordinal);

    private HashSet<string> TryGetTriggerIdentities(JsonNode payload)
    {
        try
        {
            return GetTriggerIdentities(_artifactSerializer.Deserialize(payload.ToJsonString()));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            // Compatibility validation owns payload acceptance. An older envelope
            // cannot safely participate in trigger comparison, so it remains
            // deployable rather than turning this safeguard into a new wire break.
            return [];
        }
    }

    private static RuntimeArtifactOperation ResolveOperation(string artifactType)
    {
        if (string.Equals(artifactType, "orchestration-version.new", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(artifactType, "orchestration-version-snapshot", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeArtifactOperation.Deploy;
        }

        if (string.Equals(artifactType, "orchestration-version.deprecated", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeArtifactOperation.Deprecate;
        }

        if (string.Equals(artifactType, "orchestration-version.archived", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeArtifactOperation.Archive;
        }

        throw new InvalidOperationException($"Artifact type '{artifactType}' is not supported.");
    }

    private static RuntimeReleaseStrategy ResolveStrategy(string strategy)
    {
        if (string.IsNullOrWhiteSpace(strategy) ||
            string.Equals(strategy, "Immediate", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(strategy, "Direct", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeReleaseStrategy.Direct;
        }

        if (string.Equals(strategy, "Rollout", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeReleaseStrategy.Rollout;
        }

        throw new InvalidOperationException("Release strategy must be Direct or Rollout.");
    }

    private static void EnsureDirectLifecycleStrategy(string strategy)
    {
        if (ResolveStrategy(strategy) != RuntimeReleaseStrategy.Direct)
        {
            throw new InvalidOperationException("Deprecation and archive artifacts must use the Direct release strategy.");
        }
    }

    private static RuntimeArtifactDeploymentResult Accepted(
        RuntimeOrchestrationArtifact artifact,
        RuntimeOrchestrationArtifactStatus status,
        string message)
        => new()
        {
            Accepted = true,
            RuntimeArtifactId = artifact.Id.ToString(),
            Status = status.ToString(),
            Message = message
        };

    private static string Normalize(string value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static Id ParseId(string value)
        => new(Ulid.Parse(value));

    private static SemanticVersion ParseVersion(string value)
    {
        var parts = (value ?? string.Empty).Split('.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], out var major)
            || !int.TryParse(parts[1], out var minor)
            || !int.TryParse(parts[2], out var patch))
        {
            throw new InvalidOperationException($"Semantic version '{value}' is invalid.");
        }

        return new SemanticVersion(major, minor, patch);
    }

    private enum RuntimeArtifactOperation
    {
        Deploy,
        Deprecate,
        Archive
    }

    private enum RuntimeReleaseStrategy
    {
        Direct,
        Rollout
    }
}
