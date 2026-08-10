namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Topic and version pair used to register or remove a runtime consumer.
/// </summary>
internal sealed class RuntimeArtifactConsumerBinding
{
    /// <summary>
    /// Gets the logical topic or queue.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// Gets the message contract version.
    /// </summary>
    public required string Version { get; init; }
}
