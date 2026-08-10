using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Storage;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Defines mapping operations from domain definitions to interaction models.
/// </summary>
public interface IOrchestrationDefinitionInteractionMapper
{
    /// <summary>
    /// Maps a domain definition to an interaction model.
    /// </summary>
    /// <param name="source">Source value to map.</param>
    /// <returns>Operation result.</returns>
    OrchestrationDefinitionModel ToModel(OrchestrationDefinition source);
    /// <summary>
    /// Maps a paged domain result to an interaction paged result.
    /// </summary>
    /// <param name="source">Source value to map.</param>
    /// <returns>Paged interaction result.</returns>
    InteractionPagedResult<OrchestrationDefinitionModel> ToPagedModel(PagedResult<OrchestrationDefinition> source);
}


