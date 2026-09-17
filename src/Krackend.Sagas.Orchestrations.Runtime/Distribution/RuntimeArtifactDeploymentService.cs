using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeArtifactDeploymentService"/> class.
    /// </summary>
    public RuntimeArtifactDeploymentService(
        IRuntimeArtifactRepository artifactRepository,
        IRuntimeStorageUnitOfWork unitOfWork,
        IRuntimeArtifactProjectionScheduler projectionScheduler,
        IRuntimeArtifactCompatibilityValidator compatibilityValidator)
    {
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _projectionScheduler = projectionScheduler ?? throw new ArgumentNullException(nameof(projectionScheduler));
        _compatibilityValidator = compatibilityValidator ?? throw new ArgumentNullException(nameof(compatibilityValidator));
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

        var compatibility = await _compatibilityValidator.ValidateAsync(payload, cancellationToken);
        if (!compatibility.Succeeded)
        {
            return Rejected(
                $"Artifact runtime compatibility validation failed ({compatibility.ErrorCode}): {compatibility.ErrorMessage}");
        }

        var existingArtifact = await TryGetExistingArtifact(
            package.OrchestrationDefinitionKey,
            version,
            cancellationToken);

        if (existingArtifact is not null &&
            !string.Equals(existingArtifact.ArtifactChecksum.Value, package.Checksum, StringComparison.Ordinal))
        {
            return Rejected(
                $"Artifact '{package.OrchestrationDefinitionKey}' v{package.Version} already exists with a different checksum.");
        }

        if (existingArtifact is not null &&
            existingArtifact.ArtifactChecksum.Value == package.Checksum &&
            existingArtifact.Status == RuntimeOrchestrationArtifactStatus.Ready)
        {
            return new RuntimeArtifactDeploymentResult
            {
                Accepted = true,
                RuntimeArtifactId = existingArtifact.Id.ToString(),
                Status = RuntimeOrchestrationArtifactStatus.Ready.ToString(),
                Message = $"Artifact '{existingArtifact.OrchestrationDefinitionKey}' v{existingArtifact.Version} is already ready."
            };
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
            Notes = $"Installed from '{sourceKey}' release target '{package.ReleaseTargetId}'."
        };

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        using (_unitOfWork.DeferAutoSave())
        {
            await _artifactRepository.Upsert(artifact, cancellationToken);
            await _projectionScheduler.ScheduleProjectionAsync(new RuntimeArtifactProjectionRequest
            {
                ArtifactId = artifact.Id.ToString(),
                IngressGeneration = artifact.IngressGeneration,
                RequestedBy = sourceKey,
                RequestedOnUtc = now
            }, cancellationToken);
            await _unitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new RuntimeArtifactDeploymentResult
        {
            Accepted = true,
            RuntimeArtifactId = artifact.Id.ToString(),
            Status = RuntimeOrchestrationArtifactStatus.Pending.ToString(),
            Message = $"Artifact '{artifact.OrchestrationDefinitionKey}' v{artifact.Version} accepted for ingress projection."
        };
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
}
