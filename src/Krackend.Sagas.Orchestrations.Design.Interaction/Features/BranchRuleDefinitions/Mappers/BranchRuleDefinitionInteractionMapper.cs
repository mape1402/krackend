using Krackend.Sagas.Orchestrations.Design.Core;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Maps domain definitions to interaction models.
/// </summary>
public sealed class BranchRuleDefinitionInteractionMapper : IBranchRuleDefinitionInteractionMapper
{
    /// <summary>
    /// Maps a domain definition to an interaction model.
    /// </summary>
    /// <param name="source">Source value to map.</param>
    /// <returns>Operation result.</returns>
    public BranchRuleDefinitionModel ToModel(BranchRuleDefinition source)
    {
        return new BranchRuleDefinitionModel
        {
            Id = source.Id.ToString(),
            FromType = source.FromType,
            FromId = source.FromId.ToString(),
            Condition = source.Condition,
            NavigateToType = source.NavigateToType,
            NavigateToId = source.NavigateToId.ToString(),
        };
    }
}

