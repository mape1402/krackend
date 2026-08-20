namespace Krackend.Sagas.Orchestrations.Client.Publishing;

/// <summary>
/// Publishes orchestration client messages to a transport.
/// </summary>
public interface IOrchestrationClientPublisher
{
    /// <summary>
    /// Publishes a payload to a transport destination.
    /// </summary>
    Task PublishAsync(object payload, string topic, string version, CancellationToken cancellationToken = default);
}
