using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;

namespace Krackend.Sagas.Orchestrations.Messaging.Abstractions.Consuming;

/// <summary>
/// Broker-neutral context passed to runtime message consumers.
/// </summary>
public sealed class MessageConsumeContext
{
    /// <summary>
    /// Gets the logical topic or queue where the message was received.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// Gets the message contract version declared by the envelope.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the logical producer domain when the underlying adapter provides it.
    /// </summary>
    public string From { get; init; }

    /// <summary>
    /// Gets the envelope creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedOnUtc { get; init; }

    /// <summary>
    /// Gets the business payload carried by the envelope.
    /// </summary>
    public JsonNode Message { get; init; }

    /// <summary>
    /// Gets the orchestrator metadata extracted from the envelope.
    /// </summary>
    public OrchestratorMessageMetadata Metadata { get; init; }
}
