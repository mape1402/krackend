namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

using System.Text.Json.Nodes;

/// <summary>
/// Describes a transport-specific address used to send a response back to the orchestrator.
/// </summary>
public sealed class OrchestrationReplyAddress
{
    /// <summary>
    /// Gets or sets the transport key used to resolve the client publisher adapter.
    /// </summary>
    public string Transport { get; set; }

    /// <summary>
    /// Gets or sets the transport-specific settings payload.
    /// </summary>
    public string SettingsPayload { get; set; }

    /// <summary>
    /// Gets or sets optional address metadata.
    /// </summary>
    public Dictionary<string, JsonNode> Metadata { get; set; } = new();
}
