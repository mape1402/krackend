using Krackend.Sagas.Orchestrations.Design.Core;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Defines mapping operations from domain definitions to interaction models.
/// </summary>
public interface IBranchRuleDefinitionInteractionMapper
{
    /// <summary>
    /// Maps a domain definition to an interaction model.
    /// </summary>
    /// <param name="source">Source value to map.</param>
    /// <returns>Operation result.</returns>
    BranchRuleDefinitionModel ToModel(BranchRuleDefinition source);
}

