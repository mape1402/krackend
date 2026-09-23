namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

/// <summary>
/// Default parser for orchestration ButterMorph designer context keys.
/// </summary>
public sealed class OrchestrationButterMorphDesignerContextParser : IOrchestrationButterMorphDesignerContextParser
{
    private const string TaskTransformationPrefix = "orchestration-task-transform";

    /// <inheritdoc />
    public string FormatTaskTransformation(string orchestrationVersionId, string taskDefinitionId)
    {
        if (string.IsNullOrWhiteSpace(orchestrationVersionId))
        {
            throw new ArgumentException("Orchestration version id is required.", nameof(orchestrationVersionId));
        }

        if (string.IsNullOrWhiteSpace(taskDefinitionId))
        {
            throw new ArgumentException("Task definition id is required.", nameof(taskDefinitionId));
        }

        return $"{TaskTransformationPrefix}:{orchestrationVersionId.Trim()}:{taskDefinitionId.Trim()}";
    }

    /// <inheritdoc />
    public bool TryParseTaskTransformation(string contextKey, out OrchestrationButterMorphDesignerContext context)
    {
        context = null;
        if (string.IsNullOrWhiteSpace(contextKey))
        {
            return false;
        }

        var parts = contextKey.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], TaskTransformationPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2]))
        {
            return false;
        }

        context = new OrchestrationButterMorphDesignerContext
        {
            OrchestrationVersionId = parts[1],
            TaskDefinitionId = parts[2]
        };
        return true;
    }
}
