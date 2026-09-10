namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using System.Security.Cryptography;
using System.Text;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;

/// <summary>
/// Builds deterministic schema contexts from orchestration stage and task ordering.
/// </summary>
public sealed class OrchestrationSchemaContextBuilder : IOrchestrationSchemaContextBuilder
{
    /// <inheritdoc />
    public Task<OrchestrationSchemaContext> BuildForTask(
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
        AddPreviousStageTaskResponseSources(stages, targetStage, sources);
        AddCurrentStageTaskResponseSources(targetStage, targetTask, sources);

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

        return Task.FromResult(context);
    }

    private static void AddTriggerSources(OrchestrationVersion version, ICollection<OrchestrationSchemaSource> sources)
    {
        var trigger = (version.TriggerBindings ?? [])
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .FirstOrDefault(x => x.TriggerChannel is EventTriggerChannel);

        if (trigger?.TriggerChannel is not EventTriggerChannel eventChannel || eventChannel.SchemaBinding is null)
        {
            return;
        }

        sources.Add(new OrchestrationSchemaSource
        {
            Alias = "trigger",
            SourceKind = OrchestrationSchemaContextSourceKind.Trigger,
            SchemaBinding = eventChannel.SchemaBinding
        });
    }

    private static void AddPreviousStageTaskResponseSources(
        IEnumerable<StageDefinition> stages,
        StageDefinition targetStage,
        ICollection<OrchestrationSchemaSource> sources)
    {
        foreach (var stage in stages.TakeWhile(stage => !ReferenceEquals(stage, targetStage)))
        {
            foreach (var task in GetEnabledTasks(stage))
            {
                AddTaskResponseSource(stage, task, sources);
            }
        }
    }

    private static void AddCurrentStageTaskResponseSources(
        StageDefinition stage,
        TaskDefinition targetTask,
        ICollection<OrchestrationSchemaSource> sources)
    {
        var tasks = GetEnabledTasks(stage).ToArray();
        var targetOrderBoundary = GetTargetOrderBoundary(tasks, targetTask);

        foreach (var task in tasks.Where(task => task.Order < targetOrderBoundary))
        {
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

    private static void AddTaskResponseSource(
        StageDefinition stage,
        TaskDefinition task,
        ICollection<OrchestrationSchemaSource> sources)
    {
        var responseBinding = GetResponseSchemaBinding(task);
        if (responseBinding is null)
        {
            return;
        }

        sources.Add(new OrchestrationSchemaSource
        {
            Alias = $"stages.{stage.Key}.tasks.{task.Key}.response",
            SourceKind = OrchestrationSchemaContextSourceKind.TaskResponse,
            StageKey = stage.Key,
            TaskKey = task.Key,
            SchemaBinding = responseBinding
        });
    }

    private static OrchestrationSchemaTarget CreateTarget(StageDefinition stage, TaskDefinition task)
        => new()
        {
            Alias = $"stages.{stage.Key}.tasks.{task.Key}.request",
            StageKey = stage.Key,
            TaskKey = task.Key,
            SchemaBinding = GetRequestSchemaBinding(task)
        };

    private static SchemaBinding GetRequestSchemaBinding(TaskDefinition task)
        => task.Configuration switch
        {
            MessagingTaskConfiguration messaging => messaging.RequestSchemaBinding ?? messaging.SchemaBinding,
            _ => null
        };

    private static SchemaBinding GetResponseSchemaBinding(TaskDefinition task)
        => task.Configuration switch
        {
            MessagingTaskConfiguration messaging => messaging.ResponseSchemaBinding,
            _ => null
        };

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
