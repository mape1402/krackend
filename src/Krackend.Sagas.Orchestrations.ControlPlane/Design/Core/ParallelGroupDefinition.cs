namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a parallel execution group inside a stage.
/// </summary>
public sealed class ParallelGroupDefinition
{
    /// <summary>
    /// Gets or sets id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets stage definition id.
    /// </summary>
    public Id StageDefinitionId { get; set; }

    /// <summary>
    /// Gets or sets name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets join policy.
    /// </summary>
    public ParallelJoinPolicy JoinPolicy { get; set; }

    /// <summary>
    /// Gets or sets max parallel agents.
    /// </summary>
    public int? MaxParallelAgents { get; set; }
}
