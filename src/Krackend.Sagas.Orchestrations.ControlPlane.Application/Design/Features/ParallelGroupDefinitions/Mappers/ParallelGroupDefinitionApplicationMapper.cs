using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Maps domain definitions to interaction models.
/// </summary>
public sealed class ParallelGroupDefinitionApplicationMapper : IParallelGroupDefinitionApplicationMapper
{
    /// <summary>
    /// Maps a domain definition to an interaction model.
    /// </summary>
    /// <param name="source">Source value to map.</param>
    /// <returns>Operation result.</returns>
    public ParallelGroupDefinitionModel ToModel(ParallelGroupDefinition source)
    {
        return new ParallelGroupDefinitionModel
        {
            Id = source.Id.ToString(),
            StageDefinitionId = source.StageDefinitionId.ToString(),
            Name = source.Name ?? string.Empty,
            JoinPolicy = source.JoinPolicy,
            MaxParallelAgents = source.MaxParallelAgents,
        };
    }
}

