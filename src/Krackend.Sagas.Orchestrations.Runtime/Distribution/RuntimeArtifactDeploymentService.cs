using System.Security.Cryptography;
using System.Text;
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
public sealed class RuntimeArtifactDeploymentService : IRuntimeArtifactDeploymentService, IRuntimeArtifactRollbackService
{
    private readonly IRuntimeArtifactRepository _artifactRepository;
    private readonly IRuntimeStorageUnitOfWork _unitOfWork;
    private readonly IRuntimeArtifactProjectionScheduler _projectionScheduler;
    private readonly IRuntimeArtifactCompatibilityValidator _compatibilityValidator;
    private readonly IRuntimeIngressConfigurationRepository _ingressConfigurationRepository;
    private readonly IRuntimeArtifactSerializer _artifactSerializer;
    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IRuntimeArtifactDeploymentHistoryRepository _deploymentHistoryRepository;
    private readonly IRuntimeTriggerClaimRepository _triggerClaimRepository;
    private readonly IRuntimeIngressConfigurationKeyBuilder _ingressKeyBuilder;

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
        IOrchestrationInstanceRepository instanceRepository,
        IRuntimeArtifactDeploymentHistoryRepository deploymentHistoryRepository,
        IRuntimeTriggerClaimRepository triggerClaimRepository,
        IRuntimeIngressConfigurationKeyBuilder ingressKeyBuilder = null)
    {
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _projectionScheduler = projectionScheduler ?? throw new ArgumentNullException(nameof(projectionScheduler));
        _compatibilityValidator = compatibilityValidator ?? throw new ArgumentNullException(nameof(compatibilityValidator));
        _ingressConfigurationRepository = ingressConfigurationRepository ?? throw new ArgumentNullException(nameof(ingressConfigurationRepository));
        _artifactSerializer = artifactSerializer ?? throw new ArgumentNullException(nameof(artifactSerializer));
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _deploymentHistoryRepository = deploymentHistoryRepository ?? throw new ArgumentNullException(nameof(deploymentHistoryRepository));
        _triggerClaimRepository = triggerClaimRepository ?? throw new ArgumentNullException(nameof(triggerClaimRepository));
        _ingressKeyBuilder = ingressKeyBuilder ?? new DefaultRuntimeIngressConfigurationKeyBuilder();
    }

    /// <inheritdoc />
    public async Task<RuntimeArtifactDeploymentResult> DeployAsync(
        RuntimeArtifactDeliveryPackage package,
        string sourceKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (!TryValidatePackage(
                package,
                out var packageValidationError,
                out var payload,
                out var version,
                out var artifactId,
                out var sourceVersionId,
                out var targetArtifactChecksum))
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
                    package, sourceKey, version, targetArtifactChecksum, cancellationToken),
                RuntimeArtifactOperation.Archive => await ArchiveAsync(
                    package, sourceKey, version, targetArtifactChecksum, cancellationToken),
                _ => Rejected($"Artifact type '{package.ArtifactType}' is not supported.")
            };
        }
        catch (InvalidOperationException exception)
        {
            return Rejected(exception.Message);
        }
    }

    /// <inheritdoc />
    public async Task<RuntimeArtifactDeploymentResult> RollbackAsync(string artifactId, string requestedBy, CancellationToken cancellationToken = default)
    {
        if (!Ulid.TryParse(artifactId, out var parsed)) return Rejected("Artifact id is invalid.");
        if (_projectionScheduler is not IRuntimeDurableRolloutProjectionScheduler ||
            _ingressConfigurationRepository is not IRuntimeSelectiveTriggerIngressRepository ||
            _ingressConfigurationRepository is not IRuntimeReactivatableTriggerIngressRepository ||
            _triggerClaimRepository is not IRuntimeSelectiveTriggerClaimRepository ||
            _artifactRepository is not IRuntimeArtifactRolloutTransitionRepository ||
            _deploymentHistoryRepository is not IRuntimeArtifactRollbackStatusRepository)
            return Rejected("The configured runtime storage does not support durable staged rollback.");

        RuntimeOrchestrationArtifact target;
        try { target = await _artifactRepository.GetById(new Id(parsed), cancellationToken); }
        catch (KeyNotFoundException) { return Rejected("The rollback target was not found."); }
        if (!target.IsActive || target.Status != RuntimeOrchestrationArtifactStatus.Ready ||
            target.LifecycleStatus != RuntimeOrchestrationLifecycleStatus.Active)
            return Rejected("Only an active, ready orchestration version can be used as a rollback target.");

        OrchestrationArtifact definition;
        try { definition = _artifactSerializer.Deserialize(target.ArtifactPayload.ToJsonString()); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        { return Rejected($"The rollback target cannot be read: {exception.Message}"); }
        var identities = GetTriggerIdentities(definition);
        var targetKeys = definition.TriggerBindings.Where(x => x.IsEnabled && x.TriggerChannel is EventTriggerChannelArtifact)
            .Select(_ingressKeyBuilder.BuildTriggerKey).Distinct(StringComparer.Ordinal).ToArray();
        if (identities.Count == 0 || targetKeys.Length == 0) return Rejected("The rollback target has no enabled messaging triggers.");
        var activeIngress = await _ingressConfigurationRepository.GetActiveByArtifactIdAsync(target.Id, cancellationToken);
        if (!activeIngress.Any(x => x.IngressKind == IngressKind.Backchannel))
            return Rejected("The rollback target has no active backchannel for its existing instances.");

        var conflicts = await FindTriggerConflictsAsync(target.ArtifactPayload, target.Id, cancellationToken);
        if (conflicts.Any(x => !string.Equals(x.OrchestrationDefinitionKey, target.OrchestrationDefinitionKey, StringComparison.OrdinalIgnoreCase)))
            return Rejected("A trigger belongs to a different orchestration and cannot be replaced by rollback.");
        var replacements = await BuildRollbackReplacementsAsync(identities, conflicts, cancellationToken);
        if (replacements.Length == 0)
            return Rejected("No active version with matching triggers was found to roll back.");
        var now = DateTime.UtcNow;
        var operationId = $"runtime-rollback:{Id.New()}";
        target.IngressGeneration = Math.Max(target.IngressGeneration, 0) + 1;
        target.Notes = $"Rollback requested by '{requestedBy ?? string.Empty}'.";

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        using (_unitOfWork.DeferAutoSave())
        {
            var claim = await ClaimTriggersAsync(identities, target.Id, conflicts.Select(x => x.Id).ToArray(), cancellationToken);
            if (!claim.Succeeded) return Rejected("Another deployment owns one of the target triggers. Refresh and retry.");
            await _artifactRepository.Upsert(target, cancellationToken);
            await _deploymentHistoryRepository.Append(new RuntimeArtifactDeploymentRecord
            {
                Id = Id.New(), RuntimeOrchestrationArtifactId = target.Id, OrchestrationDefinitionKey = target.OrchestrationDefinitionKey,
                OrchestrationVersion = target.Version.ToString(), ControlPlaneArtifactId = operationId, ReleaseId = operationId,
                ArtifactType = "orchestration-version.rollback-requested", ReleaseStrategy = "Rollback",
                LifecycleStatus = target.LifecycleStatus, RuntimeStatus = RuntimeOrchestrationArtifactStatus.Pending, AppliedOnUtc = now
            }, cancellationToken);
            await ScheduleProjectionAsync(target, requestedBy, now, cancellationToken, identities, replacements, targetKeys, operationId);
            await _unitOfWork.SaveChanges(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return Accepted(target, RuntimeOrchestrationArtifactStatus.Pending,
            $"Rollback to '{target.OrchestrationDefinitionKey}' v{target.Version} was requested and awaits connector preparation.");
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

        if (existingArtifact is not null && existingArtifact.Status == RuntimeOrchestrationArtifactStatus.Pending &&
            !string.IsNullOrWhiteSpace(package.ReleaseId) &&
            string.Equals(existingArtifact.LastReleaseId, package.ReleaseId, StringComparison.Ordinal) &&
            string.Equals(existingArtifact.LastControlPlaneArtifactId, package.ArtifactId, StringComparison.Ordinal))
        {
            return Accepted(existingArtifact, RuntimeOrchestrationArtifactStatus.Pending,
                $"Artifact '{existingArtifact.OrchestrationDefinitionKey}' v{existingArtifact.Version} was already accepted for ingress projection.");
        }

        var conflictingArtifacts = await FindTriggerConflictsAsync(payload, existingArtifact?.Id, cancellationToken);
        if (conflictingArtifacts.Any(x => !string.Equals(x.OrchestrationDefinitionKey, package.OrchestrationDefinitionKey, StringComparison.OrdinalIgnoreCase)))
            return Rejected("A trigger is already owned by a different orchestration; rollout cannot replace it.");
        if (conflictingArtifacts.Count > 0 && strategy == RuntimeReleaseStrategy.Direct)
        {
            return Rejected(
                "Direct deployment would create competing trigger ingress. Use Rollout to replace the active trigger subscription.");
        }

        if (conflictingArtifacts.Count > 0 && _ingressConfigurationRepository is not IRuntimeSelectiveTriggerIngressRepository)
            return Rejected("The configured ingress repository does not support selective rollout handoff.");
        if (conflictingArtifacts.Count > 0 && _projectionScheduler is not IRuntimeDurableRolloutProjectionScheduler)
            return Rejected("The configured projection scheduler does not support durable staged rollout.");

        var now = DateTime.UtcNow;
        // Every new attempt needs a distinct generation, even with the same
        // payload, so an older in-flight action cannot apply stale handoff data.
        var ingressGeneration = Math.Max(existingArtifact?.IngressGeneration ?? 0, 0) + 1;
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
            var claimResult = await ClaimTriggersAsync(
                TryGetTriggerIdentities(payload),
                artifact.Id,
                strategy == RuntimeReleaseStrategy.Rollout
                    ? conflictingArtifacts.Select(conflictingArtifact => conflictingArtifact.Id).ToArray()
                    : Array.Empty<Id>(),
                cancellationToken);
            if (!claimResult.Succeeded)
            {
                return Rejected(
                    "Another runtime deployment already owns one of these trigger identities. Refresh the release state and use Rollout only for the active owner.");
            }

            await _artifactRepository.Upsert(artifact, cancellationToken);
            await AppendHistoryAsync(artifact, package, now, cancellationToken);
            var incomingIdentities = TryGetTriggerIdentities(payload);
            var replacements = BuildRolloutReplacements(incomingIdentities, conflictingArtifacts);
            await ScheduleProjectionAsync(artifact, sourceKey, now, cancellationToken, incomingIdentities, replacements);
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
        string targetArtifactChecksum,
        CancellationToken cancellationToken)
    {
        EnsureDirectLifecycleStrategy(package.ReleaseStrategy);
        var artifact = await TryGetExistingArtifact(package.OrchestrationDefinitionKey, version, cancellationToken);
        if (artifact is null)
        {
            return Rejected($"Artifact '{package.OrchestrationDefinitionKey}' v{package.Version} is not installed in this runtime.");
        }

        if (!string.Equals(artifact.ArtifactChecksum.Value, targetArtifactChecksum, StringComparison.Ordinal))
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
            await ReleaseTriggersAsync([artifact.Id], cancellationToken);
            await _artifactRepository.Upsert(artifact, cancellationToken);
            await AppendHistoryAsync(artifact, package, now, cancellationToken);
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
        string targetArtifactChecksum,
        CancellationToken cancellationToken)
    {
        EnsureDirectLifecycleStrategy(package.ReleaseStrategy);
        var artifact = await TryGetExistingArtifact(package.OrchestrationDefinitionKey, version, cancellationToken);
        if (artifact is null)
        {
            return Rejected($"Artifact '{package.OrchestrationDefinitionKey}' v{package.Version} is not installed in this runtime.");
        }

        if (!string.Equals(artifact.ArtifactChecksum.Value, targetArtifactChecksum, StringComparison.Ordinal))
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

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        using (_unitOfWork.DeferAutoSave())
        {
            if (_artifactRepository is IRuntimeArtifactLifecycleFenceRepository fence)
            {
                if (!await fence.TryFenceLifecycle(artifact.Id, artifact.IngressGeneration, cancellationToken))
                    return Rejected("The orchestration version changed while archive was being prepared. Refresh and retry.");
                await _unitOfWork.SaveChanges(cancellationToken);
                artifact.IngressGeneration = checked(artifact.IngressGeneration + 1);
            }
            var hasActiveInstances = _instanceRepository is IOrchestrationArtifactInstanceReader reader
                ? await reader.HasActiveInstances(artifact.Id, cancellationToken)
                : (await _instanceRepository.GetRecoverable(int.MaxValue, cancellationToken))
                    .Any(x => x.RuntimeOrchestrationArtifactId == artifact.Id);
            if (hasActiveInstances)
                return Rejected("The orchestration version still has active SAGA instances or instances being processed and cannot be archived.");

            artifact.LifecycleStatus = RuntimeOrchestrationLifecycleStatus.Archived;
            artifact.LastReleaseId = package.ReleaseId ?? string.Empty;
            artifact.LastControlPlaneArtifactId = package.ArtifactId;
            artifact.ReleaseStrategy = RuntimeReleaseStrategy.Direct.ToString();
            artifact.IsActive = false;
            artifact.Status = RuntimeOrchestrationArtifactStatus.Retired;
            artifact.RetiredOnUtc = DateTime.UtcNow;
            artifact.IngressGeneration = Math.Max(artifact.IngressGeneration, 1) + 1;
            artifact.Notes = $"Archived from '{sourceKey}' release '{package.ReleaseId}' target '{package.ReleaseTargetId}'.";
            await ReleaseTriggersAsync([artifact.Id], cancellationToken);
            await _artifactRepository.Upsert(artifact, cancellationToken);
            await AppendHistoryAsync(artifact, package, artifact.RetiredOnUtc!.Value, cancellationToken);
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
        out Id sourceVersionId,
        out string targetArtifactChecksum)
    {
        error = string.Empty;
        payload = null;
        version = default;
        artifactId = default;
        sourceVersionId = default;
        targetArtifactChecksum = string.Empty;

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

            var operation = ResolveOperation(package.ArtifactType);
            if ((operation is RuntimeArtifactOperation.Deprecate or RuntimeArtifactOperation.Archive) &&
                IsLifecycleArtifact(payloadObject))
            {
                targetArtifactChecksum = ValidateLifecyclePayload(package, operation);
                return true;
            }

            if (operation == RuntimeArtifactOperation.Deploy && IsLifecycleArtifact(payloadObject))
            {
                throw new InvalidOperationException("A lifecycle action payload cannot be deployed as a new orchestration version.");
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

            var calculatedPayloadChecksum = CalculateChecksum(package.PayloadJson);
            if (package.Checksum.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                // The package checksum protects the serialized transport envelope. The checksum
                // embedded in an orchestration snapshot identifies the design snapshot and is
                // intentionally allowed to differ from the envelope hash.
                if (!string.Equals(calculatedPayloadChecksum, package.Checksum, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Artifact package checksum does not match its payload.");
                }
            }
            else if (!string.Equals(payloadChecksum, package.Checksum, StringComparison.Ordinal))
            {
                // Historical packages used the snapshot checksum as the package checksum.
                throw new InvalidOperationException(
                    $"Artifact package checksum '{package.Checksum}' does not match payload checksum '{payloadChecksum}'.");
            }

            // Artifacts created before lifecycle envelopes carried the complete snapshot. Keep accepting
            // those historical release packages while treating their snapshot checksum as the target.
            targetArtifactChecksum = package.Checksum;
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

    private static bool IsLifecycleArtifact(JsonObject payload)
        => string.Equals(
            ReadPayloadString(payload["artifactKind"] ?? payload["ArtifactKind"]),
            OrchestrationVersionLifecycleArtifact.Kind,
            StringComparison.Ordinal);

    private static string ValidateLifecyclePayload(
        RuntimeArtifactDeliveryPackage package,
        RuntimeArtifactOperation operation)
    {
        var lifecycle = JsonSerializer.Deserialize<OrchestrationVersionLifecycleArtifact>(
            package.PayloadJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Lifecycle artifact payload is empty.");

        if (!string.Equals(lifecycle.SchemaVersion, "1.0", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Lifecycle artifact schema version '{lifecycle.SchemaVersion}' is not supported.");
        }

        if (!string.Equals(lifecycle.ArtifactKind, OrchestrationVersionLifecycleArtifact.Kind, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Lifecycle artifact kind is invalid.");
        }

        EnsureRequired(lifecycle.OrchestrationDefinitionId, "Payload field 'orchestrationDefinitionId'");
        EnsureRequired(lifecycle.OrchestrationDefinitionKey, "Payload field 'orchestrationDefinitionKey'");
        EnsureRequired(lifecycle.OrchestrationVersionId, "Payload field 'orchestrationVersionId'");
        EnsureRequired(lifecycle.Version, "Payload field 'version'");

        var expectedOperation = operation switch
        {
            RuntimeArtifactOperation.Deprecate => "deprecate",
            RuntimeArtifactOperation.Archive => "archive",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported lifecycle operation."),
        };
        if (!string.Equals(lifecycle.Operation, expectedOperation, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Lifecycle artifact operation '{lifecycle.Operation}' does not match package type '{package.ArtifactType}'.");
        }

        EnsurePackageAndLifecycleMatch(
            package.OrchestrationDefinitionId,
            lifecycle.OrchestrationDefinitionId,
            "orchestration definition id");
        EnsurePackageAndLifecycleMatch(
            package.OrchestrationDefinitionKey,
            lifecycle.OrchestrationDefinitionKey,
            "orchestration definition key");
        EnsurePackageAndLifecycleMatch(
            package.OrchestrationVersionId,
            lifecycle.OrchestrationVersionId,
            "orchestration version id");
        EnsurePackageAndLifecycleMatch(package.Version, lifecycle.Version, "version");
        EnsureRequired(lifecycle.TargetArtifactChecksum, "Payload field 'targetArtifactChecksum'");

        var calculatedChecksum = CalculateChecksum(package.PayloadJson);
        if (!string.Equals(calculatedChecksum, package.Checksum, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Lifecycle artifact checksum does not match its payload.");
        }

        return lifecycle.TargetArtifactChecksum;
    }

    private static void EnsurePackageAndLifecycleMatch(string packageValue, string payloadValue, string field)
    {
        if (!string.Equals(packageValue, payloadValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Artifact package {field} '{packageValue}' does not match lifecycle payload {field} '{payloadValue}'.");
        }
    }

    private static string CalculateChecksum(string payload)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
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
        CancellationToken cancellationToken,
        IReadOnlyCollection<string> rolloutTriggerIdentities = null,
        RuntimeArtifactTriggerReplacement[] rolloutReplacements = null,
        string[] rollbackTargetKeys = null,
        string rollbackOperationId = null)
    {
        await _projectionScheduler.ScheduleProjectionAsync(new RuntimeArtifactProjectionRequest
        {
            ArtifactId = artifact.Id.ToString(),
            IngressGeneration = artifact.IngressGeneration,
            RequestedBy = sourceKey,
            RequestedOnUtc = now,
            RolloutTriggerIdentities = rolloutTriggerIdentities?.ToArray() ?? [],
            RolloutReplacements = rolloutReplacements ?? [],
            RollbackTargetTriggerKeys = rollbackTargetKeys ?? [],
            RollbackOperationId = rollbackOperationId
        }, cancellationToken);
    }

    private RuntimeArtifactTriggerReplacement[] BuildRolloutReplacements(
        HashSet<string> incomingIdentities, IReadOnlyCollection<RuntimeOrchestrationArtifact> conflictingArtifacts)
        => conflictingArtifacts.Select(previous =>
        {
            var definition = _artifactSerializer.Deserialize(previous.ArtifactPayload.ToJsonString());
            return new RuntimeArtifactTriggerReplacement
            {
                ArtifactId = previous.Id.ToString(),
                ConfigurationKeys = definition.TriggerBindings.Where(trigger => trigger.IsEnabled &&
                    trigger.TriggerChannel is EventTriggerChannelArtifact channel &&
                    incomingIdentities.Contains($"{IngressTransport.Messaging}|{Normalize(channel.Topic)}|{Normalize(channel.Version.ToString())}"))
                    .Select(_ingressKeyBuilder.BuildTriggerKey).ToArray()
            };
        }).ToArray();

    private async Task<RuntimeArtifactTriggerReplacement[]> BuildRollbackReplacementsAsync(
        HashSet<string> targetIdentities, IReadOnlyCollection<RuntimeOrchestrationArtifact> conflicts, CancellationToken cancellationToken)
    {
        var replacements = new List<RuntimeArtifactTriggerReplacement>();
        foreach (var source in conflicts)
        {
            var definition = _artifactSerializer.Deserialize(source.ArtifactPayload.ToJsonString());
            var active = (await _ingressConfigurationRepository.GetActiveByArtifactIdAsync(source.Id, cancellationToken))
                .Where(x => x.IngressKind == IngressKind.Trigger)
                .Select(x => x.ConfigurationKey).ToHashSet(StringComparer.Ordinal);
            var triggers = definition.TriggerBindings.Where(x => x.IsEnabled && x.TriggerChannel is EventTriggerChannelArtifact)
                .Select(x => (Key: _ingressKeyBuilder.BuildTriggerKey(x), Channel: (EventTriggerChannelArtifact)x.TriggerChannel))
                .Where(x => active.Contains(x.Key))
                .Select(x => (x.Key, Identity: $"{IngressTransport.Messaging}|{Normalize(x.Channel.Topic)}|{Normalize(x.Channel.Version.ToString())}"))
                .ToArray();
            if (!triggers.Any(x => targetIdentities.Contains(x.Identity))) continue;
            replacements.Add(new RuntimeArtifactTriggerReplacement
            {
                ArtifactId = source.Id.ToString(),
                ConfigurationKeys = triggers.Select(x => x.Key).Distinct(StringComparer.Ordinal).ToArray(),
                ReleaseTriggerIdentities = triggers.Select(x => x.Identity).Where(x => !targetIdentities.Contains(x))
                    .Distinct(StringComparer.Ordinal).ToArray()
            });
        }
        return replacements.ToArray();
    }

    private Task<RuntimeTriggerClaimResult> ClaimTriggersAsync(
        IReadOnlyCollection<string> identities,
        Id artifactId,
        IReadOnlyCollection<Id> replaceableArtifactIds,
        CancellationToken cancellationToken)
        => _triggerClaimRepository.ClaimAsync(identities, artifactId, replaceableArtifactIds, cancellationToken);

    private Task ReleaseTriggersAsync(IReadOnlyCollection<Id> artifactIds, CancellationToken cancellationToken)
        => _triggerClaimRepository.ReleaseAsync(artifactIds, cancellationToken);

    private Task AppendHistoryAsync(
        RuntimeOrchestrationArtifact artifact,
        RuntimeArtifactDeliveryPackage package,
        DateTime appliedOnUtc,
        CancellationToken cancellationToken)
        => _deploymentHistoryRepository.Append(new RuntimeArtifactDeploymentRecord
        {
            Id = Id.New(),
            RuntimeOrchestrationArtifactId = artifact.Id,
            OrchestrationDefinitionKey = artifact.OrchestrationDefinitionKey,
            OrchestrationVersion = artifact.Version.ToString(),
            ControlPlaneArtifactId = package.ArtifactId,
            ReleaseId = package.ReleaseId ?? string.Empty,
            ArtifactType = package.ArtifactType,
            ReleaseStrategy = artifact.ReleaseStrategy,
            LifecycleStatus = artifact.LifecycleStatus,
            RuntimeStatus = artifact.Status,
            AppliedOnUtc = appliedOnUtc
        }, cancellationToken);

    private static HashSet<string> GetTriggerIdentities(OrchestrationArtifact artifact)
        => artifact.TriggerBindings
            .Where(x => x.IsEnabled && x.TriggerChannel is EventTriggerChannelArtifact)
            .Select(x => (EventTriggerChannelArtifact)x.TriggerChannel)
            .Select(x => $"{IngressTransport.Messaging}|{Normalize(x.Topic)}|{Normalize(x.Version.ToString())}")
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
