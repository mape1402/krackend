namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
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
    private const string TriggerMetadataSourceAlias = "trigger_metadata";

    private static readonly string TriggerMetadataSchemaJson = CreateTriggerMetadataSchemaJson();
    private static readonly string TriggerMetadataContentHash = BuildContentHash(TriggerMetadataSchemaJson);

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
            .Where(x => x.IsEnabled)
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
        AddTriggerMetadataSource(sources);
        await AddMetadataSources(sources, cancellationToken);
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
            .Where(x => x.IsEnabled)
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
        AddTriggerMetadataSource(sources);
        await AddMetadataSources(sources, cancellationToken);
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

    /// <inheritdoc />
    public async Task<OrchestrationSchemaContext> BuildForTaskCompensation(
        OrchestrationVersion version,
        Id taskDefinitionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);

        cancellationToken.ThrowIfCancellationRequested();

        var stages = GetEnabledStages(version);
        var targetStage = stages.FirstOrDefault(stage => GetEnabledTasks(stage)
            .Any(task => task.Id.Equals(taskDefinitionId)));

        if (targetStage is null)
        {
            throw new InvalidOperationException($"Task '{taskDefinitionId}' was not found in orchestration version '{version.Id}'.");
        }

        var targetTask = GetEnabledTasks(targetStage).First(task => task.Id.Equals(taskDefinitionId));

        var sources = new List<OrchestrationSchemaSource>();
        AddTriggerSources(version, sources);
        AddTriggerMetadataSource(sources);
        await AddMetadataSources(sources, cancellationToken);
        AddTaskSourcesThroughTarget(stages, targetStage, targetTask, sources);

        var target = CreateCompensationTarget(targetStage, targetTask);
        var context = new OrchestrationSchemaContext
        {
            OrchestrationVersionId = version.Id.ToString(),
            OrchestrationVersion = version.Version.ToString(),
            StageKey = targetStage.Key,
            TaskKey = $"{targetTask.Key}.compensation",
            Sources = sources,
            Target = target,
            Signature = BuildSignature(sources, target)
        };

        return context;
    }

    /// <inheritdoc />
    public async Task<OrchestrationSchemaContext> BuildForTriggerCompensation(
        OrchestrationVersion version,
        Id triggerBindingId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);

        cancellationToken.ThrowIfCancellationRequested();

        var trigger = (version.TriggerBindings ?? [])
            .Where(x => x.IsEnabled)
            .FirstOrDefault(x => x.Id.Equals(triggerBindingId));

        if (trigger is null)
        {
            throw new InvalidOperationException($"Trigger '{triggerBindingId}' was not found in orchestration version '{version.Id}'.");
        }

        var sources = new List<OrchestrationSchemaSource>();
        AddTriggerSource(trigger, sources);
        AddTriggerMetadataSource(sources);
        await AddMetadataSources(sources, cancellationToken);

        var target = CreateCompensationTarget(trigger);
        var context = new OrchestrationSchemaContext
        {
            OrchestrationVersionId = version.Id.ToString(),
            OrchestrationVersion = version.Version.ToString(),
            StageKey = "trigger",
            TaskKey = $"{trigger.Key}.compensation",
            Sources = sources,
            Target = target,
            Signature = BuildSignature(sources, target)
        };

        return context;
    }

    private async Task AddMetadataSources(ICollection<OrchestrationSchemaSource> sources, CancellationToken cancellationToken)
    {
        if (_metadataDescriptorRepository is null)
        {
            return;
        }

        var descriptors = (await _metadataDescriptorRepository.GetAllDescriptors(cancellationToken))
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToArray();

        foreach (var descriptor in descriptors)
        {
            AddSource(sources, new OrchestrationSchemaSource
            {
                Alias = SanitizeAlias(descriptor.Key),
                SourceKind = OrchestrationSchemaContextSourceKind.Metadata,
                SchemaBinding = CreateMetadataSchemaBinding(descriptor)
            });
        }
    }

    private static void AddTriggerMetadataSource(ICollection<OrchestrationSchemaSource> sources)
    {
        AddSource(sources, new OrchestrationSchemaSource
        {
            Alias = TriggerMetadataSourceAlias,
            SourceKind = OrchestrationSchemaContextSourceKind.TriggerMetadata,
            SchemaBinding = CreateTriggerMetadataSchemaBinding()
        });
    }

    private static void AddTriggerSources(OrchestrationVersion version, ICollection<OrchestrationSchemaSource> sources)
    {
        var trigger = (version.TriggerBindings ?? [])
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .FirstOrDefault(x => x.TriggerChannel is EventTriggerChannel);

        AddTriggerSource(trigger, sources);
    }

    private static void AddTriggerSource(TriggerBinding trigger, ICollection<OrchestrationSchemaSource> sources)
    {
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

    private static void AddTaskSourcesThroughTarget(
        IEnumerable<StageDefinition> stages,
        StageDefinition targetStage,
        TaskDefinition targetTask,
        ICollection<OrchestrationSchemaSource> sources)
    {
        foreach (var stage in stages)
        {
            var reachedTargetStage = ReferenceEquals(stage, targetStage);
            var tasks = GetEnabledTasks(stage).ToArray();
            var taskCutoff = reachedTargetStage
                ? targetTask.Order
                : int.MaxValue;

            foreach (var task in tasks.Where(task => task.Order <= taskCutoff))
            {
                AddTaskRequestSource(stage, task, sources);
                AddTaskResponseSource(stage, task, sources);
            }

            if (reachedTargetStage)
            {
                return;
            }
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

    private static StageDefinition[] GetEnabledStages(OrchestrationVersion version)
        => (version.StageDefinitions ?? [])
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .ToArray();

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

    private static OrchestrationSchemaTarget CreateCompensationTarget(StageDefinition stage, TaskDefinition task)
        => new()
        {
            Alias = BuildCompensationRequestAlias(task.Key),
            StageKey = stage.Key,
            TaskKey = task.Key,
            SchemaBinding = GetRequestSchemaBinding(task.CompensationDefinition?.Configuration)
        };

    private static OrchestrationSchemaTarget CreateCompensationTarget(TriggerBinding trigger)
        => new()
        {
            Alias = BuildCompensationRequestAlias(trigger.Key),
            StageKey = "trigger",
            TaskKey = trigger.Key,
            SchemaBinding = GetRequestSchemaBinding(trigger.CompensationDefinition?.Configuration)
        };

    private static SchemaBinding CreateTriggerMetadataSchemaBinding()
        => new()
        {
            Id = Id.New(),
            ElementType = ElementType.Orchestration,
            ElementId = Id.New(),
            ContractId = Id.New(),
            ContractKey = OrchestrationMetadataConstants.TriggerMetadataKey,
            ContractVersion = new SemanticVersion(1, 0, 0),
            RegistryProviderId = Id.New(),
            RegistryProviderKey = "krackend",
            ContractKind = SchemaContractKind.Unspecified,
            StrictMode = false,
            IsValidationEnabled = false,
            Snapshot = new DesignSchemaContractSnapshot
            {
                ContractKind = SchemaContractKind.Unspecified,
                RegistryProviderId = "krackend",
                RegistryProviderKey = "krackend",
                ContractId = OrchestrationMetadataConstants.TriggerMetadataKey,
                ContractKey = OrchestrationMetadataConstants.TriggerMetadataKey,
                ContractVersion = "1.0.0",
                SchemaFormat = "JsonSchema",
                SchemaJson = TriggerMetadataSchemaJson,
                ContentHash = TriggerMetadataContentHash,
                SourceArtifactId = string.Empty,
                ResolvedBy = "Krackend.TriggerMetadata",
                ResolvedAtUtc = DateTimeOffset.UtcNow
            }
        };

    private static SchemaBinding CreateMetadataSchemaBinding(MetadataDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.SchemaJson))
        {
            throw new InvalidOperationException($"Metadata descriptor '{descriptor.Key}' does not contain a valid schema snapshot.");
        }

        return new SchemaBinding
        {
            Id = Id.New(),
            ElementType = ElementType.Orchestration,
            ElementId = Id.New(),
            ContractId = descriptor.Id,
            ContractKey = descriptor.Key,
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
                ContractId = descriptor.Id.ToString(),
                ContractKey = descriptor.Key,
                ContractVersion = "0.0.0",
                SchemaFormat = "JsonSchema",
                SchemaJson = descriptor.SchemaJson,
                ContentHash = descriptor.ContentHash,
                SourceArtifactId = descriptor.Id.ToString(),
                ResolvedBy = "ControlPlane.MetadataDescriptors",
                ResolvedAtUtc = DateTimeOffset.UtcNow
            }
        };
    }

    private static string CreateTriggerMetadataSchemaJson()
    {
        JsonObject Property()
            => new()
            {
                ["type"] = "string"
            };

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                [nameof(OrchestrationTriggerMetadata.CorrelationId)] = Property(),
                [nameof(OrchestrationTriggerMetadata.TraceId)] = Property(),
                [nameof(OrchestrationTriggerMetadata.EventId)] = Property(),
                [nameof(OrchestrationTriggerMetadata.EventType)] = Property(),
                [nameof(OrchestrationTriggerMetadata.IdempotencyKey)] = Property(),
                [nameof(OrchestrationTriggerMetadata.AggregateId)] = Property(),
                [nameof(OrchestrationTriggerMetadata.AggregateType)] = Property(),
                [nameof(OrchestrationTriggerMetadata.CausationId)] = Property()
            }
        };

        return schema.ToJsonString();
    }

    private static string BuildContentHash(string content)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private static SchemaBinding GetRequestSchemaBinding(TaskDefinition task)
        => GetRequestSchemaBinding(task.Configuration);

    private static SchemaBinding GetRequestSchemaBinding(ITaskConfiguration configuration)
        => configuration switch
        {
            MessagingTaskConfiguration messaging => SelectRequestBinding(messaging),
            _ => null
        };

    private static SchemaBinding GetResponseSchemaBinding(TaskDefinition task)
        => GetResponseSchemaBinding(task.Configuration);

    private static SchemaBinding GetResponseSchemaBinding(ITaskConfiguration configuration)
        => configuration switch
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

    private static string BuildCompensationRequestAlias(string ownerKey)
        => SanitizeAlias($"{ownerKey}_compensation_request");

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
