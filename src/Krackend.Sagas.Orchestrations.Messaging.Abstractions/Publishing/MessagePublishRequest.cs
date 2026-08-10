using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Messaging.Abstractions.Publishing;

/// <summary>
/// Broker-neutral request used by the runtime to publish a business message.
/// </summary>
public sealed class MessagePublishRequest
{
    /// <summary>
    /// Gets the logical topic or queue where the message should be published.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// Gets the message contract version.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the business payload to publish.
    /// </summary>
    public required JsonNode Message { get; init; }
}
