namespace Krackend.Sagas.Orchestrations.Client.Publishing;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

/// <summary>
/// Defines how an intercepted operation participates in orchestration.
/// </summary>
public sealed class OrchestrationOperationOptions
{
    /// <summary>
    /// Gets or sets the logical service that executes the operation.
    /// </summary>
    public string ServiceName { get; set; }

    /// <summary>
    /// Gets or sets the logical operation name reported to the orchestrator.
    /// </summary>
    public string OperationName { get; set; }

    /// <summary>
    /// Gets or sets the reply address used when the operation starts an orchestration.
    /// </summary>
    public OrchestrationReplyAddress TriggerAddress { get; set; }

    /// <summary>
    /// Gets a value indicating whether an explicit trigger destination was configured.
    /// </summary>
    public bool HasTriggerDestination => TriggerAddress is not null;

    /// <summary>
    /// Gets additional technical metadata attached to the execution result.
    /// </summary>
    public Dictionary<string, JsonNode> Metadata { get; } = new();
}
