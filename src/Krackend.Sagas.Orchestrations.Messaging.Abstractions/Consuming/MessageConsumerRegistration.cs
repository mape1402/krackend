namespace Krackend.Sagas.Orchestrations.Messaging.Abstractions.Consuming;

/// <summary>
/// Describes a runtime consumer binding and its broker-neutral handler.
/// </summary>
public sealed class MessageConsumerRegistration
{
    /// <summary>
    /// Gets the logical topic or queue to consume.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// Gets the message contract version to bind.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the handler invoked when a message arrives.
    /// </summary>
    public required Func<MessageConsumeContext, CancellationToken, Task> Handler { get; init; }
}
