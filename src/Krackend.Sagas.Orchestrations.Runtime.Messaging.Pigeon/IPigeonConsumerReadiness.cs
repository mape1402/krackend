namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon;

/// <summary>Confirms that a Pigeon transport consumer is actually accepting deliveries.</summary>
public interface IPigeonConsumerReadiness
{
    /// <summary>
    /// Completes only when the specified local broker consumer is ready; throws if readiness cannot be established.
    /// Implementations must observe cancellation and must not treat handler registration alone as readiness.
    /// </summary>
    Task WaitUntilReadyAsync(string topic, string version, string subscription, CancellationToken cancellationToken = default);
}
