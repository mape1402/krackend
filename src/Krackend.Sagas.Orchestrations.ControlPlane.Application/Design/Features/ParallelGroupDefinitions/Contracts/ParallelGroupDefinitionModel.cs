using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents interaction data for parallel group definition.
/// </summary>
public sealed class ParallelGroupDefinitionModel
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the stage definition id.
    /// </summary>
    public string StageDefinitionId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the join policy.
    /// </summary>
    public ParallelJoinPolicy JoinPolicy { get; set; }
    /// <summary>
    /// Gets or sets the max parallel agents.
    /// </summary>
    public int? MaxParallelAgents { get; set; }
}

