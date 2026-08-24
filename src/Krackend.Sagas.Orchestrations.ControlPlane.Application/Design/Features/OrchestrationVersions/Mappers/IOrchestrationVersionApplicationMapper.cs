using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Defines mapping operations from domain definitions to interaction models.
/// </summary>
public interface IOrchestrationVersionApplicationMapper
{
    /// <summary>
    /// Maps a domain definition to an interaction model.
    /// </summary>
    /// <param name="source">Source value to map.</param>
    /// <returns>Operation result.</returns>
    OrchestrationVersionModel ToModel(OrchestrationVersion source);
    /// <summary>
    /// Maps a paged domain result to an interaction paged result.
    /// </summary>
    /// <param name="source">Source value to map.</param>
    /// <returns>Paged interaction result.</returns>
    ApplicationPagedResult<OrchestrationVersionModel> ToPagedModel(PagedResult<OrchestrationVersion> source);
}


