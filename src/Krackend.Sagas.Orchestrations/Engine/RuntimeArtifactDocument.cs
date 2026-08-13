using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

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
    /// Gets trigger bindings promoted by Design.
    /// </summary>
    public IReadOnlyCollection<JsonObject> TriggerBindings { get; init; } = Array.Empty<JsonObject>();

    /// <summary>
    /// Gets variable definitions promoted by Design.
    /// </summary>
    public IReadOnlyCollection<JsonObject> VariableDefinitions { get; init; } = Array.Empty<JsonObject>();

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
        var version = ReadVersion(root, "Version", "version", "ArtifactVersion", "artifactVersion");
        if (string.IsNullOrWhiteSpace(version))
            version = ReadString(root, "Version", "version", "ArtifactVersion", "artifactVersion");

        var stages = ReadArray(root, "StageDefinitions", "stageDefinitions", "Stages", "stages")
            .Select(ParseStage)
            .OrderBy(x => x.Order)
            .ToArray();

        return new RuntimeArtifactDocument
        {
            Key = key,
            Version = string.IsNullOrWhiteSpace(version) ? fallbackVersion : version,
            TriggerBindings = ReadArray(root, "TriggerBindings", "triggerBindings").OfType<JsonObject>().Select(CloneObject).ToArray(),
            VariableDefinitions = ReadArray(root, "VariableDefinitions", "variableDefinitions").OfType<JsonObject>().Select(CloneObject).ToArray(),
            Stages = stages
        };
    }

    private static RuntimeStageDocument ParseStage(JsonNode node)
    {
        var obj = node.AsObject();
        return new RuntimeStageDocument
        {
            Id = ReadString(obj, "Id", "id"),
            Key = ReadString(obj, "Key", "key"),
            Order = ReadInt(obj, "Order", "order"),
            ExecutionCondition = CloneObject(ReadObject(obj, "ExecutionCondition", "executionCondition")),
            ParallelGroups = ReadArray(obj, "ParallelGroups", "parallelGroups").OfType<JsonObject>().Select(CloneObject).ToArray(),
            BranchRules = ReadArray(obj, "BranchRules", "branchRules").OfType<JsonObject>().Select(CloneObject).ToArray(),
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
        var dispatchType = ReadEnumName<TaskDispatchType>(obj, TaskDispatchType.FireAndForget, "DispatchType", "dispatchType");
        return new RuntimeTaskDocument
        {
            Key = ReadString(obj, "Key", "key"),
            Order = ReadInt(obj, "Order", "order"),
            Kind = ReadEnumName<TaskKind>(obj, TaskKind.Messaging, "Kind", "kind"),
            ExecutionMode = ReadEnumName<TaskExecutionMode>(obj, TaskExecutionMode.Sequential, "ExecutionMode", "executionMode"),
            DispatchType = dispatchType,
            ParallelGroupId = ReadString(obj, "ParallelGroupId", "parallelGroupId"),
            OnErrorPolicy = ReadEnumName<OnErrorPolicy>(obj, OnErrorPolicy.Stop, "OnErrorPolicy", "onErrorPolicy"),
            IsEnabled = ReadBool(obj, true, "IsEnabled", "isEnabled"),
            AwaitResponse = IsAwaitResponse(dispatchType),
            Destination = configuration is null
                ? string.Empty
                : ReadString(configuration, "Topic", "topic", "Destination", "destination"),
            MessageVersion = configuration is null
                ? string.Empty
                : ReadVersion(configuration, "Version", "version", "MessageVersion", "messageVersion"),
            ExecutionCondition = CloneObject(ReadObject(obj, "ExecutionCondition", "executionCondition")),
            Transformation = CloneObject(ReadObject(obj, "Transformation", "transformation")),
            Configuration = CloneObject(configuration),
            RetryPolicy = CloneObject(ReadObject(obj, "RetryPolicy", "retryPolicy")),
            TimeoutPolicy = CloneObject(ReadObject(obj, "TimeoutPolicy", "timeoutPolicy")),
            Compensation = CloneObject(ReadObject(obj, "Compensation", "compensation"))
        };
    }

    private static bool IsAwaitResponse(string dispatchType)
        => string.Equals(dispatchType, nameof(TaskDispatchType.FireAndWait), StringComparison.OrdinalIgnoreCase)
            || string.Equals(dispatchType, nameof(TaskDispatchType.FireAndWaitCallback), StringComparison.OrdinalIgnoreCase)
            || string.Equals(dispatchType, "RequestResponse", StringComparison.OrdinalIgnoreCase);

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

    private static string ReadVersion(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            var node = obj[name];
            if (node is null)
                continue;

            if (node is JsonValue value && value.TryGetValue<string>(out var text))
                return text ?? string.Empty;

            if (node is JsonObject version)
            {
                var major = ReadInt(version, "Major", "major");
                var minor = ReadInt(version, "Minor", "minor");
                var patch = ReadInt(version, "Patch", "patch");
                return $"{major}.{minor}.{patch}";
            }
        }

        return string.Empty;
    }

    private static string ReadEnumName<TEnum>(JsonObject obj, TEnum defaultValue, params string[] names)
        where TEnum : struct, Enum
    {
        foreach (var name in names)
        {
            var node = obj[name];
            if (node is null)
                continue;

            if (node is JsonValue value)
            {
                if (value.TryGetValue<string>(out var text)
                    && Enum.TryParse<TEnum>(text, true, out var parsedText))
                    return parsedText.ToString();

                if (value.TryGetValue<int>(out var number)
                    && Enum.IsDefined(typeof(TEnum), number))
                    return ((TEnum)Enum.ToObject(typeof(TEnum), number)).ToString();
            }
        }

        return defaultValue.ToString();
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

    private static JsonObject CloneObject(JsonObject obj)
        => obj?.DeepClone().AsObject() ?? new JsonObject();
}
