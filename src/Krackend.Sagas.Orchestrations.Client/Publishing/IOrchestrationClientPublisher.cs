namespace Krackend.Sagas.Orchestrations.Client.Publishing;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

/// <summary>
/// Publishes orchestration client messages to a transport.
/// </summary>
public interface IOrchestrationClientPublisher
{
    /// <summary>
    /// Publishes a payload to a transport destination.
    /// </summary>
    /// <param name="payload">Business payload to publish.</param>
    /// <param name="address">Transport-specific destination address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous publish operation.</returns>
    Task PublishAsync(object payload, OrchestrationReplyAddress address, CancellationToken cancellationToken = default);
}
