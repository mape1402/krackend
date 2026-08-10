using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Engine;

internal sealed class RuntimeArtifactDocument
{
    /// <summary>
    /// Gets the orchestration key declared by the artifact.
    /// </summary>
    public string Key { get; init; }

    /// <summary>
    /// Gets the orchestration artifact version used for runtime back-channel versioning.
    /// </summary>
    public string Version { get; init; }

    /// <summary>
    /// Gets executable stages ordered by artifact order.
    /// </summary>
    public IReadOnlyCollection<RuntimeStageDocument> Stages { get; init; } = Array.Empty<RuntimeStageDocument>();

    /// <summary>
    /// Parses a runtime artifact payload into the engine execution document.
    /// </summary>
    /// <param name="payload">Runtime artifact payload.</param>
    /// <param name="fallbackVersion">Version provided by runtime storage when the payload does not include it.</param>
    /// <returns>Parsed runtime artifact document.</returns>
    public static RuntimeArtifactDocument Parse(JsonNode payload, string fallbackVersion = "")
    {
        var root = payload?.AsObject() ?? throw new InvalidOperationException("Runtime artifact payload is required.");
        var key = ReadString(root, "Key", "key", "OrchestrationDefinitionKey", "orchestrationDefinitionKey");
        var version = ReadString(root, "Version", "version", "ArtifactVersion", "artifactVersion");
        var stages = ReadArray(root, "StageDefinitions", "stageDefinitions", "Stages", "stages")
            .Select(ParseStage)
            .OrderBy(x => x.Order)
            .ToArray();

        return new RuntimeArtifactDocument
        {
            Key = key,
            Version = string.IsNullOrWhiteSpace(version) ? fallbackVersion : version,
            Stages = stages
        };
    }

    private static RuntimeStageDocument ParseStage(JsonNode node)
    {
        var obj = node.AsObject();
        return new RuntimeStageDocument
        {
            Key = ReadString(obj, "Key", "key"),
            Order = ReadInt(obj, "Order", "order"),
            Tasks = ReadArray(obj, "TaskDefinitions", "taskDefinitions", "Tasks", "tasks")
                .Select(ParseTask)
                .Where(x => x.IsEnabled)
                .OrderBy(x => x.Order)
                .ToArray()
        };
    }

    private static RuntimeTaskDocument ParseTask(JsonNode node)
    {
        var obj = node.AsObject();
        var configuration = ReadObject(obj, "Configuration", "configuration");
        return new RuntimeTaskDocument
        {
            Key = ReadString(obj, "Key", "key"),
            Order = ReadInt(obj, "Order", "order"),
            Kind = ReadString(obj, "Kind", "kind"),
            ExecutionMode = ReadString(obj, "ExecutionMode", "executionMode"),
            IsEnabled = ReadBool(obj, true, "IsEnabled", "isEnabled"),
            AwaitResponse = IsAwaitResponse(ReadString(obj, "ExecutionMode", "executionMode")),
            Destination = configuration is null
                ? string.Empty
                : ReadString(configuration, "Topic", "topic", "Destination", "destination"),
            MessageVersion = configuration is null
                ? string.Empty
                : ReadString(configuration, "Version", "version", "MessageVersion", "messageVersion")
        };
    }

    private static bool IsAwaitResponse(string executionMode)
        => string.Equals(executionMode, "FireAndWaitCallback", StringComparison.OrdinalIgnoreCase)
            || string.Equals(executionMode, "RequestResponse", StringComparison.OrdinalIgnoreCase);

    private static JsonObject ReadObject(JsonObject obj, params string[] names)
        => names.Select(name => obj[name]).OfType<JsonObject>().FirstOrDefault();

    private static IEnumerable<JsonNode> ReadArray(JsonObject obj, params string[] names)
        => names.Select(name => obj[name]).OfType<JsonArray>().FirstOrDefault() ?? Enumerable.Empty<JsonNode>();

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

            if (node is JsonObject nested && nested["Value"] is JsonValue valueNode)
            {
                return valueNode.TryGetValue<string>(out var value)
                    ? value
                    : valueNode.ToString();
            }

            return node.ToString();
        }

        return string.Empty;
    }

    private static int ReadInt(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj[name] is JsonValue value && value.TryGetValue<int>(out var result))
            {
                return result;
            }
        }

        return 0;
    }

    private static bool ReadBool(JsonObject obj, bool defaultValue, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj[name] is JsonValue value && value.TryGetValue<bool>(out var result))
            {
                return result;
            }
        }

        return defaultValue;
    }
}
