using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Maps domain definitions to interaction models.
/// </summary>
public sealed class StageDefinitionApplicationMapper : IStageDefinitionApplicationMapper
{
    /// <summary>
    /// Maps a domain definition to an interaction model.
    /// </summary>
    /// <param name="source">Source value to map.</param>
    /// <returns>Operation result.</returns>
    public StageDefinitionModel ToModel(StageDefinition source)
    {
        return new StageDefinitionModel
        {
            Id = source.Id.ToString(),
            OrchestrationVersionId = source.OrchestrationVersionId.ToString(),
            Key = source.Key,
            Name = source.Name,
            Description = source.Description ?? string.Empty,
            Order = source.Order,
            ExecutionCondition = source.ExecutionCondition,
            HasExecutionCondition = source.HasExecutionCondition,
        };
    }
}

