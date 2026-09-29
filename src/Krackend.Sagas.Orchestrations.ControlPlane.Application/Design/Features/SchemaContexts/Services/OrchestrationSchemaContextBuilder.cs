namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using DesignSchemaContractSnapshot = Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.SchemaContractSnapshot;

/// <summary>
/// Builds deterministic schema contexts from orchestration stage and task ordering.
/// </summary>
public sealed class OrchestrationSchemaContextBuilder : IOrchestrationSchemaContextBuilder
{
    private readonly IMetadataDescriptorRepository _metadataDescriptorRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationSchemaContextBuilder"/> class.
    /// </summary>
    public OrchestrationSchemaContextBuilder()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationSchemaContextBuilder"/> class.
    /// </summary>
    /// <param name="metadataDescriptorRepository">Metadata descriptor repository dependency.</param>
    public OrchestrationSchemaContextBuilder(IMetadataDescriptorRepository metadataDescriptorRepository)
    {
        _metadataDescriptorRepository = metadataDescriptorRepository;
    }

    /// <inheritdoc />
    public async Task<OrchestrationSchemaContext> BuildForTask(
        OrchestrationVersion version,
        Id taskDefinitionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);

        cancellationToken.ThrowIfCancellationRequested();

        var stages = (version.StageDefinitions ?? [])
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .ToArray();

        var targetStage = stages.FirstOrDefault(stage => (stage.TaskDefinitions ?? [])
            .Any(task => task.Id.Equals(taskDefinitionId)));

        if (targetStage is null)
        {
            throw new InvalidOperationException($"Task '{taskDefinitionId}' was not found in orchestration version '{version.Id}'.");
        }

        var targetTask = (targetStage.TaskDefinitions ?? [])
            .First(task => task.Id.Equals(taskDefinitionId));

        var sources = new List<OrchestrationSchemaSource>();
        AddTriggerSources(version, sources);
        await AddMetadataSource(sources, cancellationToken);
        AddPreviousStageTaskSources(stages, targetStage, sources);
        AddCurrentStageTaskSources(targetStage, targetTask, sources);

        var target = CreateTarget(targetStage, targetTask);
        var context = new OrchestrationSchemaContext
        {
            OrchestrationVersionId = version.Id.ToString(),
            OrchestrationVersion = version.Version.ToString(),
            StageKey = targetStage.Key,
            TaskKey = targetTask.Key,
            Sources = sources,
            Target = target,
            Signature = BuildSignature(sources, target)
        };

        return context;
    }

    /// <inheritdoc />
    public async Task<OrchestrationSchemaContext> BuildForStage(
        OrchestrationVersion version,
        Id stageDefinitionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);

        cancellationToken.ThrowIfCancellationRequested();

        var stages = (version.StageDefinitions ?? [])
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .ToArray();

        var targetStage = stages.FirstOrDefault(stage => stage.Id.Equals(stageDefinitionId));
        if (targetStage is null)
        {
            throw new InvalidOperationException($"Stage '{stageDefinitionId}' was not found in orchestration version '{version.Id}'.");
        }

        var sources = new List<OrchestrationSchemaSource>();
        AddTriggerSources(version, sources);
        await AddMetadataSource(sources, cancellationToken);
        AddPreviousStageTaskSources(stages, targetStage, sources);

        var context = new OrchestrationSchemaContext
        {
            OrchestrationVersionId = version.Id.ToString(),
            OrchestrationVersion = version.Version.ToString(),
            StageKey = targetStage.Key,
            TaskKey = string.Empty,
            Sources = sources,
            Target = null,
            Signature = BuildSignature(sources, null)
        };

        return context;
    }

    private async Task AddMetadataSource(ICollection<OrchestrationSchemaSource> sources, CancellationToken cancellationToken)
    {
        if (_metadataDescriptorRepository is null)
        {
            return;
        }

        var descriptors = (await _metadataDescriptorRepository.GetAllDescriptors(cancellationToken))
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToArray();
        if (descriptors.Length == 0)
        {
            return;
        }

        AddSource(sources, new OrchestrationSchemaSource
        {
            Alias = "metadata",
            SourceKind = OrchestrationSchemaContextSourceKind.Metadata,
            SchemaBinding = CreateMetadataSchemaBinding(descriptors)
        });
    }

    private static void AddTriggerSources(OrchestrationVersion version, ICollection<OrchestrationSchemaSource> sources)
    {
        var trigger = (version.TriggerBindings ?? [])
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .FirstOrDefault(x => x.TriggerChannel is EventTriggerChannel);

        if (trigger?.TriggerChannel is not EventTriggerChannel eventChannel ||
            !IsUsableBindingReference(eventChannel.SchemaBinding))
        {
            return;
        }

        AddSource(sources, new OrchestrationSchemaSource
        {
            Alias = "trigger",
            SourceKind = OrchestrationSchemaContextSourceKind.Trigger,
            SchemaBinding = eventChannel.SchemaBinding
        });
    }

    private static void AddPreviousStageTaskSources(
        IEnumerable<StageDefinition> stages,
        StageDefinition targetStage,
        ICollection<OrchestrationSchemaSource> sources)
    {
        foreach (var stage in stages.TakeWhile(stage => !ReferenceEquals(stage, targetStage)))
        {
            foreach (var task in GetEnabledTasks(stage))
            {
                AddTaskRequestSource(stage, task, sources);
                AddTaskResponseSource(stage, task, sources);
            }
        }
    }

    private static void AddCurrentStageTaskSources(
        StageDefinition stage,
        TaskDefinition targetTask,
        ICollection<OrchestrationSchemaSource> sources)
    {
        var tasks = GetEnabledTasks(stage).ToArray();
        var targetOrderBoundary = GetTargetOrderBoundary(tasks, targetTask);

        foreach (var task in tasks.Where(task => task.Order < targetOrderBoundary))
        {
            AddTaskRequestSource(stage, task, sources);
            AddTaskResponseSource(stage, task, sources);
        }
    }

    private static int GetTargetOrderBoundary(IEnumerable<TaskDefinition> tasks, TaskDefinition targetTask)
    {
        if (!targetTask.ParallelGroupId.HasValue)
        {
            return targetTask.Order;
        }

        return tasks
            .Where(task => task.ParallelGroupId.HasValue && task.ParallelGroupId.Value.Equals(targetTask.ParallelGroupId.Value))
            .Select(task => task.Order)
            .DefaultIfEmpty(targetTask.Order)
            .Min();
    }

    private static IEnumerable<TaskDefinition> GetEnabledTasks(StageDefinition stage)
        => (stage.TaskDefinitions ?? [])
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Key, StringComparer.Ordinal);

    private static void AddTaskRequestSource(
        StageDefinition stage,
        TaskDefinition task,
        ICollection<OrchestrationSchemaSource> sources)
    {
        var requestBinding = GetRequestSchemaBinding(task);
        if (!IsUsableBindingReference(requestBinding))
        {
            return;
        }

        AddSource(sources, new OrchestrationSchemaSource
        {
            Alias = BuildTaskRequestAlias(task.Key),
            SourceKind = OrchestrationSchemaContextSourceKind.TaskRequest,
            StageKey = stage.Key,
            TaskKey = task.Key,
            SchemaBinding = requestBinding
        });
    }

    private static void AddTaskResponseSource(
        StageDefinition stage,
        TaskDefinition task,
        ICollection<OrchestrationSchemaSource> sources)
    {
        var responseBinding = GetResponseSchemaBinding(task);
        if (!HasUsableSnapshot(responseBinding))
        {
            return;
        }

        AddSource(sources, new OrchestrationSchemaSource
        {
            Alias = BuildTaskReplyAlias(task.Key),
            SourceKind = OrchestrationSchemaContextSourceKind.TaskResponse,
            StageKey = stage.Key,
            TaskKey = task.Key,
            SchemaBinding = responseBinding
        });
    }

    private static OrchestrationSchemaTarget CreateTarget(StageDefinition stage, TaskDefinition task)
        => new()
        {
            Alias = BuildTaskRequestAlias(task.Key),
            StageKey = stage.Key,
            TaskKey = task.Key,
            SchemaBinding = GetRequestSchemaBinding(task)
        };

    private static SchemaBinding CreateMetadataSchemaBinding(IReadOnlyCollection<MetadataDescriptor> descriptors)
    {
        var properties = new JsonObject();
        foreach (var descriptor in descriptors)
        {
            properties[descriptor.Key] = JsonNode.Parse(descriptor.SchemaJson)
                ?? throw new InvalidOperationException($"Metadata descriptor '{descriptor.Key}' does not contain a valid schema snapshot.");
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = true,
            ["properties"] = properties
        };
        var schemaJson = schema.ToJsonString();
        var contentHash = BuildMetadataContentHash(descriptors);

        return new SchemaBinding
        {
            Id = Id.New(),
            ElementType = ElementType.Orchestration,
            ElementId = Id.New(),
            ContractId = Id.New(),
            ContractKey = "metadata",
            ContractVersion = new SemanticVersion(0, 0, 0),
            RegistryProviderId = Id.New(),
            RegistryProviderKey = "control-plane",
            ContractKind = SchemaContractKind.Unspecified,
            StrictMode = false,
            IsValidationEnabled = false,
            Snapshot = new DesignSchemaContractSnapshot
            {
                ContractKind = SchemaContractKind.Unspecified,
                RegistryProviderId = "control-plane",
                RegistryProviderKey = "control-plane",
                ContractId = "metadata",
                ContractKey = "metadata",
                ContractVersion = "0.0.0",
                SchemaFormat = "JsonSchema",
                SchemaJson = schemaJson,
                ContentHash = contentHash,
                SourceArtifactId = string.Empty,
                ResolvedBy = "ControlPlane.MetadataDescriptors",
                ResolvedAtUtc = DateTimeOffset.UtcNow
            }
        };
    }

    private static string BuildMetadataContentHash(IEnumerable<MetadataDescriptor> descriptors)
    {
        var builder = new StringBuilder();
        foreach (var descriptor in descriptors.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            builder.Append(descriptor.Key).Append('|');
            builder.Append(descriptor.ContentHash).Append('|');
            builder.Append(descriptor.SchemaJson).AppendLine();
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static SchemaBinding GetRequestSchemaBinding(TaskDefinition task)
        => task.Configuration switch
        {
            MessagingTaskConfiguration messaging => SelectRequestBinding(messaging),
            _ => null
        };

    private static SchemaBinding GetResponseSchemaBinding(TaskDefinition task)
        => task.Configuration switch
        {
            MessagingTaskConfiguration messaging => messaging.ResponseSchemaBinding,
            _ => null
        };

    private static SchemaBinding SelectRequestBinding(MessagingTaskConfiguration messaging)
    {
        if (HasUsableSnapshot(messaging.RequestSchemaBinding))
        {
            return messaging.RequestSchemaBinding;
        }

        if (HasUsableSnapshot(messaging.SchemaBinding))
        {
            return messaging.SchemaBinding;
        }

        return IsUsableBindingReference(messaging.RequestSchemaBinding)
            ? messaging.RequestSchemaBinding
            : messaging.SchemaBinding;
    }

    private static bool IsUsableBindingReference(SchemaBinding binding)
        => binding is not null &&
            (!string.IsNullOrWhiteSpace(binding.ContractKey) || HasUsableSnapshot(binding));

    private static bool HasUsableSnapshot(SchemaBinding binding)
        => !string.IsNullOrWhiteSpace(binding?.Snapshot?.SchemaJson) ||
            !string.IsNullOrWhiteSpace(binding?.Snapshot?.ContentHash);

    private static string BuildTaskRequestAlias(string taskKey)
        => SanitizeAlias(taskKey);

    private static string BuildTaskReplyAlias(string taskKey)
        => SanitizeAlias($"{taskKey}_reply");

    private static void AddSource(
        ICollection<OrchestrationSchemaSource> sources,
        OrchestrationSchemaSource source)
    {
        var alias = EnsureUniqueAlias(sources, source);
        sources.Add(source with { Alias = alias });
    }

    private static string EnsureUniqueAlias(
        IEnumerable<OrchestrationSchemaSource> sources,
        OrchestrationSchemaSource source)
    {
        var alias = source.Alias;
        if (!sources.Any(existing => string.Equals(existing.Alias, alias, StringComparison.Ordinal)))
        {
            return alias;
        }

        alias = SanitizeAlias($"{source.StageKey}_{source.Alias}");
        var candidate = alias;
        var index = 2;

        while (sources.Any(existing => string.Equals(existing.Alias, candidate, StringComparison.Ordinal)))
        {
            candidate = $"{alias}_{index}";
            index++;
        }

        return candidate;
    }

    private static string SanitizeAlias(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = false;

        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
                continue;
            }

            if (!previousWasSeparator)
            {
                builder.Append('_');
                previousWasSeparator = true;
            }
        }

        var alias = builder.ToString().Trim('_');
        if (string.IsNullOrWhiteSpace(alias))
        {
            return "payload";
        }

        return char.IsDigit(alias[0]) ? $"p_{alias}" : alias;
    }

    private static string BuildSignature(
        IReadOnlyCollection<OrchestrationSchemaSource> sources,
        OrchestrationSchemaTarget target)
    {
        var builder = new StringBuilder();

        foreach (var source in sources.OrderBy(x => x.Alias, StringComparer.Ordinal))
        {
            AppendBinding(builder, source.Alias, source.SchemaBinding);
        }

        AppendBinding(builder, target?.Alias ?? "target", target?.SchemaBinding);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void AppendBinding(StringBuilder builder, string alias, SchemaBinding binding)
    {
        builder.Append(alias).Append('|');
        builder.Append(binding?.ContractKind.ToString() ?? string.Empty).Append('|');
        builder.Append(binding?.RegistryProviderKey ?? string.Empty).Append('|');
        builder.Append(binding?.ContractKey ?? string.Empty).Append('|');
        builder.Append(binding?.ContractVersion.ToString() ?? string.Empty).Append('|');
        builder.Append(binding?.Snapshot?.ContentHash ?? string.Empty).AppendLine();
    }
}
