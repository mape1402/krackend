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
    private readonly IRuntimeIngressSynchronizer _ingressSynchronizer;

    public RuntimeArtifactDeploymentService(RuntimeEnvironmentDescriptor runtimeEnvironment, IRuntimeArtifactRepository artifactRepository, IRuntimeIngressSynchronizer ingressSynchronizer)
    {
        _runtimeEnvironment = runtimeEnvironment ?? throw new ArgumentNullException(nameof(runtimeEnvironment));
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
        _ingressSynchronizer = ingressSynchronizer ?? throw new ArgumentNullException(nameof(ingressSynchronizer));
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
            EnvironmentKey = _runtimeEnvironment.EnvironmentKey,
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

        await _ingressSynchronizer.SynchronizeArtifact(artifact, cancellationToken);

        return RuntimeArtifactDeploymentResult.Accept(
            artifact.Id.ToString(),
            string.Empty,
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

        var topologyError = ValidateExecutableTopology(request, payload);
        if (topologyError is not null)
        {
            return topologyError;
        }

        return payload is null ? "PayloadJson must contain JSON." : null;
    }

    private static string ValidateExecutableTopology(RuntimeArtifactDeploymentRequest request, JsonNode payload)
    {
        if (payload is null || !ActivatesRuntimeConfiguration(NormalizeArtifactType(request.ArtifactType)))
        {
            return null;
        }

        var root = payload.AsObject();
        var stages = ReadArray(root, "StageDefinitions", "stageDefinitions", "Stages", "stages")
            .OfType<JsonObject>()
            .ToArray();
        var duplicatedStageOrder = stages
            .GroupBy(stage => ReadInt(stage, "Order", "order"))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicatedStageOrder is not null)
        {
            return $"Runtime artifact '{request.OrchestrationDefinitionKey}' has duplicated stage order '{duplicatedStageOrder.Key}'.";
        }

        foreach (var stage in stages)
        {
            var enabledTasks = ReadArray(stage, "TaskDefinitions", "taskDefinitions", "Tasks", "tasks")
                .OfType<JsonObject>()
                .Where(task => ReadBool(task, true, "IsEnabled", "isEnabled"))
                .ToArray();
            var duplicatedTaskOrder = enabledTasks
                .GroupBy(task => ReadInt(task, "Order", "order"))
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicatedTaskOrder is not null)
            {
                return $"Runtime artifact '{request.OrchestrationDefinitionKey}' stage '{ReadString(stage, "Key", "key")}' has duplicated task order '{duplicatedTaskOrder.Key}'.";
            }
        }

        return null;
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

    private static IEnumerable<JsonNode> ReadArray(JsonObject obj, params string[] names)
        => names.Select(name => obj[name]).OfType<JsonArray>().FirstOrDefault() ?? Enumerable.Empty<JsonNode>();

    private static int ReadInt(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            var node = obj[name];
            if (node is null)
            {
                continue;
            }

            if (node is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            if (int.TryParse(node.ToString(), out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    private static bool ReadBool(JsonObject obj, bool defaultValue, params string[] names)
    {
        foreach (var name in names)
        {
            var node = obj[name];
            if (node is null)
            {
                continue;
            }

            if (node is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var boolValue))
            {
                return boolValue;
            }

            if (bool.TryParse(node.ToString(), out var parsed))
            {
                return parsed;
            }
        }

        return defaultValue;
    }

    private static string ReadString(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            var node = obj[name];
            if (node is null)
            {
                continue;
            }

            if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue))
            {
                return stringValue ?? string.Empty;
            }

            return node.ToString();
        }

        return string.Empty;
    }
}
