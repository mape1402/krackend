namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Abstractions.Extensions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Default runtime artifact migrator that normalizes legacy linear artifacts to the extension-ready schema.
/// </summary>
public sealed class DefaultOrchestrationArtifactMigrator : IOrchestrationArtifactMigrator
{
    /// <inheritdoc />
    public OrchestrationArtifact Migrate(OrchestrationArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var migratedStages = (artifact.StageDefinitions ?? Array.Empty<StageArtifact>())
            .Select(MigrateStage)
            .ToArray();
        var requiredCapabilities = MergeRequiredCapabilities(
            artifact,
            migratedStages,
            artifact.TriggerBindings ?? Array.Empty<TriggerBindingArtifact>());

        return artifact with
        {
            ArtifactSchemaVersion = OrchestrationArtifactSchemaVersions.Current,
            ExecutionPolicy = artifact.ExecutionPolicy ?? ExecutionPolicyArtifact.Empty,
            RequiredCapabilities = requiredCapabilities,
            StageDefinitions = migratedStages
        };
    }

    private static StageArtifact MigrateStage(StageArtifact stage)
        => stage with
        {
            ExecutionPolicy = stage.ExecutionPolicy ?? ExecutionPolicyArtifact.Empty,
            TaskDefinitions = (stage.TaskDefinitions ?? Array.Empty<TaskArtifact>())
                .Select(MigrateTask)
                .ToArray()
        };

    private static TaskArtifact MigrateTask(TaskArtifact task)
    {
        var extensionKey = string.IsNullOrWhiteSpace(task.ExtensionKey)
            ? ExtensionConstants.BuiltInExtensionKey
            : task.ExtensionKey.Trim();
        var capabilityKey = string.IsNullOrWhiteSpace(task.CapabilityKey)
            ? ResolveBuiltInTaskCapability(task.Kind)
            : task.CapabilityKey.Trim();

        return task with
        {
            ExtensionKey = extensionKey,
            CapabilityKey = capabilityKey,
            CapabilityVersion = string.IsNullOrWhiteSpace(task.CapabilityVersion)
                ? "1.0.0"
                : task.CapabilityVersion.Trim(),
            ExecutionPolicy = task.ExecutionPolicy ?? ExecutionPolicyArtifact.Empty,
            RuntimeRequirements = task.RuntimeRequirements ?? ExecutionRuntimeRequirementsArtifact.Empty
        };
    }

    private static IReadOnlyList<RequiredCapabilityArtifact> MergeRequiredCapabilities(
        OrchestrationArtifact artifact,
        IReadOnlyCollection<StageArtifact> stages,
        IReadOnlyCollection<TriggerBindingArtifact> triggers)
    {
        var capabilities = new Dictionary<string, RequiredCapabilityArtifact>(StringComparer.OrdinalIgnoreCase);

        foreach (var capability in artifact.RequiredCapabilities ?? Array.Empty<RequiredCapabilityArtifact>())
        {
            if (string.IsNullOrWhiteSpace(capability.ExtensionKey) ||
                string.IsNullOrWhiteSpace(capability.CapabilityKey))
            {
                continue;
            }

            capabilities[BuildCapabilityKey(capability.ExtensionKey, capability.CapabilityKey)] = capability;
        }

        foreach (var trigger in triggers.Where(trigger => trigger.IsEnabled))
        {
            if (trigger.TriggerType == TriggerType.Event)
            {
                AddCapability(
                    capabilities,
                    ExtensionConstants.BuiltInExtensionKey,
                    BuiltInCapabilityKeys.EventTrigger,
                    artifact.Version,
                    "Trigger");
            }
        }

        foreach (var task in stages.SelectMany(stage => stage.TaskDefinitions ?? Array.Empty<TaskArtifact>()))
        {
            AddCapability(
                capabilities,
                string.IsNullOrWhiteSpace(task.ExtensionKey) ? ExtensionConstants.BuiltInExtensionKey : task.ExtensionKey,
                string.IsNullOrWhiteSpace(task.CapabilityKey) ? ResolveBuiltInTaskCapability(task.Kind) : task.CapabilityKey,
                TryParseVersion(task.CapabilityVersion) ?? artifact.Version,
                "Task");

            if (task.Compensation is not null)
            {
                AddCapability(
                    capabilities,
                    ExtensionConstants.BuiltInExtensionKey,
                    ResolveBuiltInTaskCapability(task.Compensation.CompensationTaskKind),
                    artifact.Version,
                    "Compensation");
            }
        }

        return capabilities.Values
            .OrderBy(capability => capability.ExtensionKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(capability => capability.CapabilityKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddCapability(
        IDictionary<string, RequiredCapabilityArtifact> capabilities,
        string extensionKey,
        string capabilityKey,
        SemanticVersion version,
        string kind)
    {
        if (string.IsNullOrWhiteSpace(extensionKey) || string.IsNullOrWhiteSpace(capabilityKey))
        {
            return;
        }

        var key = BuildCapabilityKey(extensionKey, capabilityKey);
        capabilities.TryAdd(key, new RequiredCapabilityArtifact(
            extensionKey.Trim(),
            capabilityKey.Trim(),
            version,
            kind));
    }

    private static string BuildCapabilityKey(string extensionKey, string capabilityKey)
        => $"{extensionKey.Trim()}::{capabilityKey.Trim()}";

    private static string ResolveBuiltInTaskCapability(TaskKind kind)
        => kind switch
        {
            TaskKind.Messaging => BuiltInCapabilityKeys.MessagingTask,
            TaskKind.Http => BuiltInCapabilityKeys.HttpTask,
            TaskKind.Plugin => BuiltInCapabilityKeys.PluginTask,
            TaskKind.HumanApproval => BuiltInCapabilityKeys.HumanApprovalTask,
            _ => $"task.{kind.ToString().ToLowerInvariant()}"
        };

    private static SemanticVersion? TryParseVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            !int.TryParse(parts[2], out var patch))
        {
            return null;
        }

        return new SemanticVersion(major, minor, patch);
    }
}
