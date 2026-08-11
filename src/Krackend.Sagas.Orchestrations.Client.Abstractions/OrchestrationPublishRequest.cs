namespace Krackend.Sagas.Orchestrations.Client.Abstractions;

/// <summary>
/// Broker-neutral publish request produced by the orchestration client.
/// </summary>
public sealed class OrchestrationPublishRequest
{
    /// <summary>
    /// Gets the output topic or queue.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// Gets the output contract version.
    /// </summary>
    public required OrchestrationSemanticVersion Version { get; init; }

    /// <summary>
    /// Gets the payload to publish.
    /// </summary>
    public required object Payload { get; init; }

    /// <summary>
    /// Gets orchestration metadata to attach to the outgoing message, when available.
    /// </summary>
    public OrchestrationClientMetadata Metadata { get; init; }
}
