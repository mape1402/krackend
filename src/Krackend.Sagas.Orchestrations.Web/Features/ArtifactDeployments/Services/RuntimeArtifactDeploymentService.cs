using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime;

namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Materializes deployable orchestration artifacts into runtime storage.
/// </summary>
public sealed class RuntimeArtifactDeploymentService : IRuntimeArtifactDeploymentService
{
    private readonly RuntimeEnvironmentDescriptor _runtimeEnvironment;
    private readonly IRuntimeArtifactRepository _artifactRepository;
    private readonly IRuntimeArtifactConsumerSynchronizer _consumerSynchronizer;

    public RuntimeArtifactDeploymentService(RuntimeEnvironmentDescriptor runtimeEnvironment, IRuntimeArtifactRepository artifactRepository, IRuntimeArtifactConsumerSynchronizer consumerSynchronizer)
    {
        _runtimeEnvironment = runtimeEnvironment ?? throw new ArgumentNullException(nameof(runtimeEnvironment));
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
        _consumerSynchronizer = consumerSynchronizer ?? throw new ArgumentNullException(nameof(consumerSynchronizer));
    }

    public async Task<RuntimeArtifactDeploymentResult> Deploy(RuntimeArtifactDeploymentRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = Validate(request, out var payload);
        if (validationError is not null)
        {
            return RuntimeArtifactDeploymentResult.Reject(request?.EnvironmentKey, validationError, request?.CorrelationId);
        }

        var artifactType = NormalizeArtifactType(request.ArtifactType);
        var activatesRuntimeConfiguration = ActivatesRuntimeConfiguration(artifactType);
        var artifact = new RuntimeOrchestrationArtifact
        {
            Id = ParseIdOrNew(request.ArtifactId),
            EnvironmentKey = request.EnvironmentKey.Trim(),
            OrchestrationDefinitionKey = request.OrchestrationDefinitionKey.Trim(),
            ArtifactType = artifactType,
            SourceOrchestrationVersionId = ParseRequiredId(request.OrchestrationVersionId),
            Version = ParseSemanticVersion(request.Version),
            ArtifactChecksum = new Checksum(request.Checksum.Trim()),
            ArtifactPayload = payload,
            IsActive = activatesRuntimeConfiguration,
            LoadedToCache = false,
            DeployedOnUtc = request.PromotedOnUtc == default ? DateTime.UtcNow : request.PromotedOnUtc,
            ActivatedOnUtc = activatesRuntimeConfiguration ? DateTime.UtcNow : null,
            Notes = $"ArtifactType={artifactType}; PromotedBy={request.PromotedBy}; CorrelationId={request.CorrelationId}; SchemaVersion={request.SchemaVersion}"
        };

        await _artifactRepository.Upsert(artifact, cancellationToken);
        if (activatesRuntimeConfiguration)
        {
            await _artifactRepository.DeactivateActiveArtifacts(
                artifact.EnvironmentKey,
                artifact.OrchestrationDefinitionKey,
                artifact.Id,
                cancellationToken);
        }

        await _consumerSynchronizer.Synchronize(artifact, cancellationToken);

        return RuntimeArtifactDeploymentResult.Accept(
            artifact.Id.ToString(),
            artifact.EnvironmentKey,
            "Activated",
            "Runtime artifact accepted and activated.",
            request.CorrelationId);
    }

    public async Task<RuntimeArtifactModel> GetActive(string orchestrationDefinitionKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orchestrationDefinitionKey))
        {
            return null;
        }

        var artifact = await _artifactRepository.GetActive(
            _runtimeEnvironment.EnvironmentKey,
            orchestrationDefinitionKey,
            cancellationToken);

        return artifact is null
            ? null
            : Map(artifact);
    }

    public async Task<IReadOnlyCollection<RuntimeArtifactModel>> GetAll(CancellationToken cancellationToken = default)
    {
        var artifacts = await _artifactRepository.GetAll(_runtimeEnvironment.EnvironmentKey, cancellationToken);
        return artifacts.Select(Map).ToArray();
    }

    private string Validate(RuntimeArtifactDeploymentRequest request, out JsonNode payload)
    {
        payload = null;

        if (request is null)
        {
            return "Deployment request is required.";
        }

        if (string.IsNullOrWhiteSpace(request.EnvironmentKey))
        {
            return "EnvironmentKey is required.";
        }

        if (!string.Equals(request.EnvironmentKey.Trim(), _runtimeEnvironment.EnvironmentKey, StringComparison.Ordinal))
        {
            return $"Artifact target environment '{request.EnvironmentKey}' does not match runtime environment '{_runtimeEnvironment.EnvironmentKey}'.";
        }

        if (string.IsNullOrWhiteSpace(request.OrchestrationVersionId))
        {
            return "OrchestrationVersionId is required.";
        }

        if (string.IsNullOrWhiteSpace(request.OrchestrationDefinitionKey))
        {
            return "OrchestrationDefinitionKey is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return "Version is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Checksum))
        {
            return "Checksum is required.";
        }

        if (string.IsNullOrWhiteSpace(request.PayloadJson))
        {
            return "PayloadJson is required.";
        }

        try
        {
            _ = ParseRequiredId(request.OrchestrationVersionId);
            _ = ParseSemanticVersion(request.Version);
            payload = JsonNode.Parse(request.PayloadJson);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return ex.Message;
        }

        return payload is null ? "PayloadJson must contain JSON." : null;
    }

    private static Id ParseIdOrNew(string value)
        => string.IsNullOrWhiteSpace(value) ? Id.New() : ParseRequiredId(value);

    private static Id ParseRequiredId(string value)
        => new(Ulid.Parse(value));

    private static SemanticVersion ParseSemanticVersion(string value)
    {
        var parts = value.Split('.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], out var major)
            || !int.TryParse(parts[1], out var minor)
            || !int.TryParse(parts[2], out var patch))
        {
            throw new FormatException("Version must use semantic format major.minor.patch.");
        }

        return new SemanticVersion(major, minor, patch);
    }

    private static RuntimeArtifactModel Map(RuntimeOrchestrationArtifact artifact)
        => new()
        {
            Id = artifact.Id.ToString(),
            EnvironmentKey = artifact.EnvironmentKey,
            OrchestrationDefinitionKey = artifact.OrchestrationDefinitionKey,
            ArtifactType = artifact.ArtifactType,
            SourceOrchestrationVersionId = artifact.SourceOrchestrationVersionId.ToString(),
            Version = artifact.Version.ToString(),
            Checksum = artifact.ArtifactChecksum.Value,
            IsActive = artifact.IsActive,
            DeployedOnUtc = artifact.DeployedOnUtc,
            ActivatedOnUtc = artifact.ActivatedOnUtc,
            RetiredOnUtc = artifact.RetiredOnUtc
        };

    private static string NormalizeArtifactType(string value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();

    private static bool ActivatesRuntimeConfiguration(string artifactType)
        => string.Equals(artifactType, "orchestration.deploy", StringComparison.OrdinalIgnoreCase);
}
