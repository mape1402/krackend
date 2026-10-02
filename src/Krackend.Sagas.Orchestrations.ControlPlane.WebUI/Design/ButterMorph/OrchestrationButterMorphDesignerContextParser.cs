namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

/// <summary>
/// Default parser for orchestration ButterMorph designer context keys.
/// </summary>
public sealed class OrchestrationButterMorphDesignerContextParser : IOrchestrationButterMorphDesignerContextParser
{
    private const string TaskTransformationPrefix = "orchestration-task-transform";
    private const string TaskCompensationTransformationPrefix = "orchestration-task-compensation-transform";
    private const string TriggerCompensationTransformationPrefix = "orchestration-trigger-compensation-transform";
    private const string TaskExecutionConditionPrefix = "orchestration-task-execution-condition";
    private const string TaskCompensationExecutionConditionPrefix = "orchestration-task-compensation-execution-condition";
    private const string TriggerCompensationExecutionConditionPrefix = "orchestration-trigger-compensation-execution-condition";
    private const string StageExecutionConditionPrefix = "orchestration-stage-execution-condition";

    /// <inheritdoc />
    public string FormatTaskTransformation(string orchestrationVersionId, string taskDefinitionId)
    {
        return Format(TaskTransformationPrefix, orchestrationVersionId, taskDefinitionId, nameof(taskDefinitionId));
    }

    /// <inheritdoc />
    public string FormatTaskCompensationTransformation(string orchestrationVersionId, string taskDefinitionId)
    {
        return Format(TaskCompensationTransformationPrefix, orchestrationVersionId, taskDefinitionId, nameof(taskDefinitionId));
    }

    /// <inheritdoc />
    public string FormatTriggerCompensationTransformation(string orchestrationVersionId, string triggerBindingId)
    {
        return Format(TriggerCompensationTransformationPrefix, orchestrationVersionId, triggerBindingId, nameof(triggerBindingId));
    }

    /// <inheritdoc />
    public string FormatTaskExecutionCondition(string orchestrationVersionId, string taskDefinitionId)
    {
        return Format(TaskExecutionConditionPrefix, orchestrationVersionId, taskDefinitionId, nameof(taskDefinitionId));
    }

    /// <inheritdoc />
    public string FormatTaskCompensationExecutionCondition(string orchestrationVersionId, string taskDefinitionId)
    {
        return Format(TaskCompensationExecutionConditionPrefix, orchestrationVersionId, taskDefinitionId, nameof(taskDefinitionId));
    }

    /// <inheritdoc />
    public string FormatTriggerCompensationExecutionCondition(string orchestrationVersionId, string triggerBindingId)
    {
        return Format(TriggerCompensationExecutionConditionPrefix, orchestrationVersionId, triggerBindingId, nameof(triggerBindingId));
    }

    /// <inheritdoc />
    public string FormatStageExecutionCondition(string orchestrationVersionId, string stageDefinitionId)
    {
        return Format(StageExecutionConditionPrefix, orchestrationVersionId, stageDefinitionId, nameof(stageDefinitionId));
    }

    /// <inheritdoc />
    public bool TryParseTaskTransformation(string contextKey, out OrchestrationButterMorphDesignerContext context)
    {
        return TryParseTaskContext(contextKey, TaskTransformationPrefix, out context);
    }

    /// <inheritdoc />
    public bool TryParseTaskCompensationTransformation(string contextKey, out OrchestrationButterMorphDesignerContext context)
    {
        return TryParseTaskContext(contextKey, TaskCompensationTransformationPrefix, out context);
    }

    /// <inheritdoc />
    public bool TryParseTriggerCompensationTransformation(string contextKey, out OrchestrationButterMorphDesignerContext context)
    {
        return TryParseTriggerContext(contextKey, TriggerCompensationTransformationPrefix, out context);
    }

    /// <inheritdoc />
    public bool TryParseTaskExecutionCondition(string contextKey, out OrchestrationButterMorphDesignerContext context)
    {
        return TryParseTaskContext(contextKey, TaskExecutionConditionPrefix, out context);
    }

    /// <inheritdoc />
    public bool TryParseTaskCompensationExecutionCondition(string contextKey, out OrchestrationButterMorphDesignerContext context)
    {
        return TryParseTaskContext(contextKey, TaskCompensationExecutionConditionPrefix, out context);
    }

    /// <inheritdoc />
    public bool TryParseTriggerCompensationExecutionCondition(string contextKey, out OrchestrationButterMorphDesignerContext context)
    {
        return TryParseTriggerContext(contextKey, TriggerCompensationExecutionConditionPrefix, out context);
    }

    /// <inheritdoc />
    public bool TryParseStageExecutionCondition(string contextKey, out OrchestrationButterMorphDesignerContext context)
    {
        context = null;
        if (!TryParse(contextKey, StageExecutionConditionPrefix, out var versionId, out var stageId))
        {
            return false;
        }

        context = new OrchestrationButterMorphDesignerContext
        {
            OrchestrationVersionId = versionId,
            StageDefinitionId = stageId
        };
        return true;
    }

    private static bool TryParseTriggerContext(
        string contextKey,
        string prefix,
        out OrchestrationButterMorphDesignerContext context)
    {
        context = null;
        if (!TryParse(contextKey, prefix, out var versionId, out var triggerId))
        {
            return false;
        }

        context = new OrchestrationButterMorphDesignerContext
        {
            OrchestrationVersionId = versionId,
            TriggerBindingId = triggerId
        };
        return true;
    }

    private static bool TryParseTaskContext(
        string contextKey,
        string prefix,
        out OrchestrationButterMorphDesignerContext context)
    {
        context = null;
        if (!TryParse(contextKey, prefix, out var versionId, out var taskId))
        {
            return false;
        }

        context = new OrchestrationButterMorphDesignerContext
        {
            OrchestrationVersionId = versionId,
            TaskDefinitionId = taskId
        };
        return true;
    }

    private static bool TryParse(
        string contextKey,
        string prefix,
        out string orchestrationVersionId,
        out string elementId)
    {
        orchestrationVersionId = string.Empty;
        elementId = string.Empty;

        if (string.IsNullOrWhiteSpace(contextKey))
        {
            return false;
        }

        var parts = contextKey.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2]))
        {
            return false;
        }

        orchestrationVersionId = parts[1];
        elementId = parts[2];
        return true;
    }

    private static string Format(
        string prefix,
        string orchestrationVersionId,
        string elementId,
        string elementParameterName)
    {
        if (string.IsNullOrWhiteSpace(orchestrationVersionId))
        {
            throw new ArgumentException("Orchestration version id is required.", nameof(orchestrationVersionId));
        }

        if (string.IsNullOrWhiteSpace(elementId))
        {
            throw new ArgumentException("Element id is required.", elementParameterName);
        }

        return $"{prefix}:{orchestrationVersionId.Trim()}:{elementId.Trim()}";
    }
}
