using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Builds compensation actions from the promoted artifact and completed runtime tasks.
/// </summary>
internal sealed class RuntimeCompensationPlanBuilder : IRuntimeCompensationPlanBuilder
{
    /// <inheritdoc/>
    public IReadOnlyCollection<RuntimeCompensationPlanItem> Build(
        RuntimeArtifactDocument document,
        IReadOnlyCollection<TaskExecution> completedTasks)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));

        if (completedTasks is null || completedTasks.Count == 0)
            return Array.Empty<RuntimeCompensationPlanItem>();

        var completedByKey = completedTasks
            .Where(x => x.Status is TaskExecutionStatus.Completed or TaskExecutionStatus.CompletedWithErrors)
            .GroupBy(x => x.TaskKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(task => task.CompletedOnUtc).First(), StringComparer.OrdinalIgnoreCase);

        var configuredTasks = document.Stages
            .OrderByDescending(stage => stage.Order)
            .SelectMany(stage => stage.Tasks.OrderByDescending(task => task.Order))
            .Where(task => completedByKey.ContainsKey(task.Key))
            .Select(task => new { Task = task, Execution = completedByKey[task.Key], Compensation = ParseCompensation(task) })
            .Where(x => x.Compensation.IsConfigured)
            .ToArray();

        return configuredTasks
            .Select(x => new RuntimeCompensationPlanItem(
                x.Execution.Id,
                x.Task.Key,
                $"compensate:{x.Task.Key}",
                x.Compensation.Kind,
                x.Compensation.DispatchType,
                x.Compensation.Destination,
                x.Compensation.MessageVersion,
                (x.Execution.OutputVariablesPayload ?? x.Execution.Metadata.GetValueOrDefault("requestPayload") ?? new JsonObject()).DeepClone(),
                new Dictionary<string, JsonNode>
                {
                    ["sourceTaskKey"] = x.Task.Key,
                    ["compensationKind"] = x.Compensation.Kind,
                    ["dispatchType"] = x.Compensation.DispatchType,
                    ["destination"] = x.Compensation.Destination,
                    ["messageVersion"] = x.Compensation.MessageVersion
                }))
            .ToArray();
    }

    private static RuntimeCompensationConfiguration ParseCompensation(RuntimeTaskDocument task)
    {
        var compensation = task.Compensation;
        if (compensation is null || compensation.Count == 0)
            return RuntimeCompensationConfiguration.Empty;

        var kind = ReadEnumName<TaskKind>(compensation, TaskKind.Messaging, "CompensationTaskKind", "compensationTaskKind");
        var dispatchType = ReadEnumName<TaskDispatchType>(compensation, TaskDispatchType.FireAndForget, "DispatchType", "dispatchType");
        var configuration = ReadObject(compensation, "Configuration", "configuration");
        var destination = configuration is null ? string.Empty : ReadString(configuration, "Topic", "topic", "Destination", "destination");
        var version = configuration is null ? string.Empty : ReadVersion(configuration, "Version", "version", "MessageVersion", "messageVersion");
        var isConfigured = !string.IsNullOrWhiteSpace(destination)
            && string.Equals(kind, nameof(TaskKind.Messaging), StringComparison.OrdinalIgnoreCase);

        return new RuntimeCompensationConfiguration(
            isConfigured,
            kind,
            dispatchType,
            destination,
            string.IsNullOrWhiteSpace(version) ? "1.0.0" : version);
    }

    private static JsonObject ReadObject(JsonObject obj, params string[] names)
        => names.Select(name => obj[name]).OfType<JsonObject>().FirstOrDefault();

    private static string ReadString(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            var node = obj[name];
            if (node is JsonValue value && value.TryGetValue<string>(out var text))
                return text ?? string.Empty;
        }

        return string.Empty;
    }

    private static string ReadVersion(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            var node = obj[name];
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

    private static int ReadInt(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj[name] is JsonValue value && value.TryGetValue<int>(out var result))
                return result;
        }

        return 0;
    }

    private static string ReadEnumName<TEnum>(JsonObject obj, TEnum defaultValue, params string[] names)
        where TEnum : struct, Enum
    {
        foreach (var name in names)
        {
            var node = obj[name];
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

    private sealed record RuntimeCompensationConfiguration(
        bool IsConfigured,
        string Kind,
        string DispatchType,
        string Destination,
        string MessageVersion)
    {
        public static RuntimeCompensationConfiguration Empty { get; } = new(false, string.Empty, string.Empty, string.Empty, string.Empty);
    }
}
