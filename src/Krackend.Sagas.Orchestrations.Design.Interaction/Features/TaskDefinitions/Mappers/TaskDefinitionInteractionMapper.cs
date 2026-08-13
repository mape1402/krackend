using Krackend.Sagas.Orchestrations.Design.Core;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Maps domain definitions to interaction models.
/// </summary>
public sealed class TaskDefinitionInteractionMapper : ITaskDefinitionInteractionMapper
{
    /// <summary>
    /// Maps a domain definition to an interaction model.
    /// </summary>
    /// <param name="source">Source value to map.</param>
    /// <returns>Operation result.</returns>
    public TaskDefinitionModel ToModel(TaskDefinition source)
    {
        return new TaskDefinitionModel
        {
            Id = source.Id.ToString(),
            StageDefinitionId = source.StageDefinitionId.ToString(),
            Key = source.Key,
            Name = source.Name,
            Order = source.Order,
            Notes = source.Notes ?? string.Empty,
            Kind = source.Kind,
            ExecutionMode = source.ExecutionMode,
            ParallelGroupId = source.ParallelGroupId.HasValue ? source.ParallelGroupId.Value.ToString() : string.Empty,
            ExecutionCondition = source.ExecutionCondition,
            HasExecutionCondition = source.HasExecutionCondition,
            Transformation = source.Transformation,
            HasTransformation = source.HasTransformation,
            Configuration = source.Configuration,
            RetryPolicy = source.RetryPolicy,
            TimeoutPolicy = source.TimeoutPolicy,
            OnErrorPolicy = source.OnErrorPolicy,
            CompensationDefinition = source.CompensationDefinition,
            DispatchType = source.DispatchType,
            IsEnabled = source.IsEnabled,
        };
    }
}

