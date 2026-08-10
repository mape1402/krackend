namespace Krackend.Sagas.Orchestrations.Messaging.Abstractions.Consuming;

/// <summary>
/// Registers and removes logical message consumers for the runtime without exposing a concrete broker implementation.
/// </summary>
public interface IMessageConsumerRegistry
{
    /// <summary>
    /// Registers a consumer handler for a specific topic and message version.
    /// </summary>
    /// <param name="registration">Consumer registration metadata and handler.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Register(
        MessageConsumerRegistration registration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a consumer handler for a specific topic and message version.
    /// </summary>
    /// <param name="topic">Logical topic or queue to stop consuming.</param>
    /// <param name="version">Message contract version to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Remove(
        string topic,
        string version,
        CancellationToken cancellationToken = default);
}
