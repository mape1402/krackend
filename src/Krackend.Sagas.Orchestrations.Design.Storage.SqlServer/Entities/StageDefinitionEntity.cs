using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Entities;

/// <summary>
/// Represents StageDefinitionEntity.
/// </summary>
public sealed class StageDefinitionEntity
{
    /// <summary>
    /// Gets or sets Id.
    /// </summary>
    public Id Id { get; set; }
    /// <summary>
    /// Gets or sets OrchestrationVersionId.
    /// </summary>
    public Id OrchestrationVersionId { get; set; }
    /// <summary>
    /// Gets or sets Key.
    /// </summary>
    public string Key { get; set; }
    /// <summary>
    /// Gets or sets Name.
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// Gets or sets Order.
    /// </summary>
    public int Order { get; set; }
    /// <summary>
    /// Gets or sets Description.
    /// </summary>
    public string Description { get; set; }
    /// <summary>
    /// Gets or sets ExecutionCondition.
    /// </summary>
    public ExecutionConditionJsonModel ExecutionCondition { get; set; } = new();
}
