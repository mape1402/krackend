namespace Krackend.Sagas.Orchestrations.Client.Abstractions;

/// <summary>
/// Per-call output destination used when no orchestrator metadata is available.
/// </summary>
public sealed class OrchestrationOutputDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationOutputDescriptor"/> class.
    /// </summary>
    public OrchestrationOutputDescriptor(string topic, OrchestrationSemanticVersion? version = null)
    {
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic is required.", nameof(topic));

        Topic = topic;
        Version = version ?? OrchestrationSemanticVersion.Default;
    }

    /// <summary>
    /// Gets the logical topic or queue where output should be published.
    /// </summary>
    public string Topic { get; }

    /// <summary>
    /// Gets the message contract version.
    /// </summary>
    public OrchestrationSemanticVersion Version { get; }
}
