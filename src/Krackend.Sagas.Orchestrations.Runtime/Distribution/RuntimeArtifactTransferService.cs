using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Default portable runtime artifact service. Imported packages always use the same
/// deployment path as a control-plane release, so trigger ownership and lifecycle
/// safeguards remain authoritative.
/// </summary>
public sealed class RuntimeArtifactTransferService : IRuntimeArtifactTransferService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly IRuntimeArtifactRepository _artifactRepository;
    private readonly IRuntimeArtifactDeploymentService _deploymentService;

    /// <summary>Initializes a new instance of the service.</summary>
    public RuntimeArtifactTransferService(IRuntimeArtifactRepository artifactRepository, IRuntimeArtifactDeploymentService deploymentService)
    {
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
        _deploymentService = deploymentService ?? throw new ArgumentNullException(nameof(deploymentService));
    }

    /// <inheritdoc />
    public async Task<RuntimeArtifactTransferPackage> ExportAsync(string artifactId, CancellationToken cancellationToken = default)
    {
        var id = ParseId(artifactId, "Runtime artifact id");
        var artifact = await _artifactRepository.GetById(id, cancellationToken)
            ?? throw new InvalidOperationException("The runtime artifact was not found.");
        var package = new RuntimeArtifactTransferPackage
        {
            ExportedOnUtc = DateTime.UtcNow,
            SourceArtifactId = artifact.Id.ToString(),
            Delivery = CreateDelivery(artifact)
        };
        package.PackageChecksum = CalculateChecksum(SerializeManifest(package));
        return package;
    }

    /// <inheritdoc />
    public string Serialize(RuntimeArtifactTransferPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        ValidatePackage(package);
        return JsonSerializer.Serialize(package, JsonOptions);
    }

    /// <inheritdoc />
    public RuntimeArtifactTransferInspection Inspect(string packageJson)
    {
        var package = DeserializeAndValidate(packageJson);
        return new RuntimeArtifactTransferInspection(package.SchemaVersion, package.PackageKind, package.SourceArtifactId,
            package.Delivery.OrchestrationDefinitionKey, package.Delivery.Version, package.Delivery.ArtifactType,
            package.Delivery.ReleaseStrategy, package.ExportedOnUtc);
    }

    /// <inheritdoc />
    public async Task<RuntimeArtifactDeploymentResult> ImportAsync(string packageJson, string importedBy, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(importedBy)) throw new ArgumentException("An import actor is required.", nameof(importedBy));
        var package = DeserializeAndValidate(packageJson);
        return await _deploymentService.DeployAsync(package.Delivery, $"portable-import:{importedBy.Trim()}", cancellationToken);
    }

    private static RuntimeArtifactDeliveryPackage CreateDelivery(RuntimeOrchestrationArtifact artifact)
        => artifact.LifecycleStatus switch
        {
            RuntimeOrchestrationLifecycleStatus.Active => CreateDeploymentDelivery(artifact),
            RuntimeOrchestrationLifecycleStatus.Deprecated => CreateLifecycleDelivery(artifact, "orchestration-version.deprecated", "deprecate"),
            RuntimeOrchestrationLifecycleStatus.Archived => CreateLifecycleDelivery(artifact, "orchestration-version.archived", "archive"),
            _ => throw new InvalidOperationException($"Runtime artifact lifecycle '{artifact.LifecycleStatus}' is not supported.")
        };

    private static RuntimeArtifactDeliveryPackage CreateDeploymentDelivery(RuntimeOrchestrationArtifact artifact)
    {
        var payloadJson = artifact.ArtifactPayload?.ToJsonString() ?? throw new InvalidOperationException("The runtime artifact does not contain a payload.");
        return CreateDeliveryBase(
            artifact,
            artifact.ArtifactType,
            artifact.ReleaseStrategy,
            payloadJson,
            ReadOrchestrationDefinitionId(artifact));
    }

    private static RuntimeArtifactDeliveryPackage CreateLifecycleDelivery(RuntimeOrchestrationArtifact artifact, string artifactType, string operation)
    {
        var definitionId = ReadOrchestrationDefinitionId(artifact);
        var lifecycle = new OrchestrationVersionLifecycleArtifact
        {
            Operation = operation,
            OrchestrationDefinitionId = definitionId,
            OrchestrationDefinitionKey = artifact.OrchestrationDefinitionKey,
            OrchestrationVersionId = artifact.SourceOrchestrationVersionId.ToString(),
            Version = artifact.Version.ToString(),
            TargetArtifactChecksum = artifact.ArtifactChecksum.Value
        };
        return CreateDeliveryBase(
            artifact,
            artifactType,
            "Direct",
            JsonSerializer.Serialize(lifecycle, JsonOptions),
            definitionId);
    }

    private static RuntimeArtifactDeliveryPackage CreateDeliveryBase(
        RuntimeOrchestrationArtifact artifact,
        string artifactType,
        string releaseStrategy,
        string payloadJson,
        string definitionId)
        => new()
        {
            ReleaseId = string.IsNullOrWhiteSpace(artifact.LastReleaseId) ? $"portable-{artifact.Id}" : artifact.LastReleaseId,
            ReleaseTargetId = $"portable-{artifact.Id}", ArtifactId = artifact.Id.ToString(), ArtifactType = artifactType,
            ReleaseStrategy = string.IsNullOrWhiteSpace(releaseStrategy) ? "Direct" : releaseStrategy, SchemaVersion = "1.0",
            OrchestrationDefinitionId = definitionId, OrchestrationVersionId = artifact.SourceOrchestrationVersionId.ToString(),
            OrchestrationDefinitionKey = artifact.OrchestrationDefinitionKey, Version = artifact.Version.ToString(),
            Checksum = CalculateChecksum(payloadJson), PayloadJson = payloadJson, CorrelationId = $"portable-{artifact.Id}",
            PromotedBy = "portable-export", PromotedOnUtc = DateTime.UtcNow
        };

    private static string ReadOrchestrationDefinitionId(RuntimeOrchestrationArtifact artifact)
    {
        var payload = artifact.ArtifactPayload
            ?? throw new InvalidOperationException("The runtime artifact does not contain a payload.");
        var definitionId = payload["orchestrationDefinitionId"]?.GetValue<string>()
            ?? payload["OrchestrationDefinitionId"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(definitionId))
        {
            throw new InvalidOperationException(
                "The runtime artifact payload does not contain its orchestration definition id.");
        }

        return definitionId;
    }

    private static RuntimeArtifactTransferPackage DeserializeAndValidate(string packageJson)
    {
        if (string.IsNullOrWhiteSpace(packageJson)) throw new InvalidOperationException("Runtime artifact package is empty.");
        try
        {
            var package = JsonSerializer.Deserialize<RuntimeArtifactTransferPackage>(packageJson, JsonOptions)
                ?? throw new InvalidOperationException("Runtime artifact package is invalid.");
            ValidatePackage(package);
            return package;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Runtime artifact package is invalid JSON.", exception);
        }
    }

    private static void ValidatePackage(RuntimeArtifactTransferPackage package)
    {
        if (package.SchemaVersion != RuntimeArtifactTransferPackage.CurrentSchemaVersion)
            throw new InvalidOperationException($"Runtime artifact package schema version '{package.SchemaVersion}' is not supported.");
        if (!string.Equals(package.PackageKind, RuntimeArtifactTransferPackage.Kind, StringComparison.Ordinal))
            throw new InvalidOperationException("Runtime artifact package kind is invalid.");
        EnsureRequired(package.SourceArtifactId, "Source artifact id");
        var delivery = package.Delivery ?? throw new InvalidOperationException("Runtime artifact package delivery is required.");
        EnsureRequired(delivery.ArtifactId, "Delivery artifact id"); EnsureRequired(delivery.ArtifactType, "Delivery artifact type");
        EnsureRequired(delivery.OrchestrationDefinitionKey, "Delivery orchestration key"); EnsureRequired(delivery.OrchestrationVersionId, "Delivery orchestration version id");
        EnsureRequired(delivery.Version, "Delivery version"); EnsureRequired(delivery.Checksum, "Delivery checksum"); EnsureRequired(delivery.PayloadJson, "Delivery payload");
        EnsureRequired(package.PackageChecksum, "Package checksum");
        if (!string.Equals(CalculateChecksum(SerializeManifest(package)), package.PackageChecksum, StringComparison.Ordinal))
            throw new InvalidOperationException("Runtime artifact package checksum does not match its contents.");
    }

    private static string SerializeManifest(RuntimeArtifactTransferPackage package)
        => JsonSerializer.Serialize(new RuntimeArtifactTransferManifest(package.SchemaVersion, package.PackageKind, package.ExportedOnUtc, package.SourceArtifactId, package.Delivery), JsonOptions);

    private static void EnsureRequired(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"{field} is required.");
    }

    private static Id ParseId(string value, string field)
    {
        if (!Ulid.TryParse(value, out var parsed)) throw new ArgumentException($"{field} is invalid.", nameof(value));
        return new Id(parsed);
    }

    private static string CalculateChecksum(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private sealed record RuntimeArtifactTransferManifest(int SchemaVersion, string PackageKind, DateTime ExportedOnUtc, string SourceArtifactId, RuntimeArtifactDeliveryPackage Delivery);
}
