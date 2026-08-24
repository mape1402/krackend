using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;

/// <summary>
/// Represents ParallelGroupDefinitionEntity.
/// </summary>
public sealed class ParallelGroupDefinitionEntity
{
    /// <summary>
    /// Gets or sets Id.
    /// </summary>
    public Id Id { get; set; }
    /// <summary>
    /// Gets or sets StageDefinitionId.
    /// </summary>
    public Id StageDefinitionId { get; set; }
    /// <summary>
    /// Gets or sets Name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets JoinPolicy.
    /// </summary>
    public ParallelJoinPolicy JoinPolicy { get; set; }
    /// <summary>
    /// Gets or sets MaxParallelAgents.
    /// </summary>
    public int? MaxParallelAgents { get; set; }
}
