namespace Krackend.Sagas.Orchestrations.Client.Publishing;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

/// <summary>
/// Defines how an intercepted operation participates in orchestration.
/// </summary>
public sealed class OrchestrationOperationOptions
{
    /// <summary>
    /// Gets or sets the reply address used when the operation starts an orchestration.
    /// </summary>
    public OrchestrationReplyAddress TriggerAddress { get; set; }

    /// <summary>
    /// Gets a value indicating whether an explicit trigger destination was configured.
    /// </summary>
    public bool HasTriggerDestination => TriggerAddress is not null;
}
