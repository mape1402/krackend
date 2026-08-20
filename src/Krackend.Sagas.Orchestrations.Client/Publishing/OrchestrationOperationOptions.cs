namespace Krackend.Sagas.Orchestrations.Client.Publishing;

/// <summary>
/// Defines how an intercepted operation participates in orchestration.
/// </summary>
public sealed class OrchestrationOperationOptions
{
    /// <summary>
    /// Gets or sets the topic used when the operation starts an orchestration.
    /// </summary>
    public string Topic { get; set; }

    /// <summary>
    /// Gets or sets the topic version used when the operation starts an orchestration.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets a value indicating whether an explicit trigger destination was configured.
    /// </summary>
    public bool HasTriggerDestination => !string.IsNullOrWhiteSpace(Topic);
}
