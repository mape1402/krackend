using Krackend.EventSourcing.Metadata;

namespace Krackend.EventSourcing.Configuration;

/// <summary>
/// Configures Krackend event sourcing services.
/// </summary>
public sealed class EventSourcingOptions
{
    /// <summary>
    /// Gets envelope configuration.
    /// </summary>
    public EventEnvelopeOptions Envelope { get; } = new();

    /// <summary>
    /// Gets logical event store configuration.
    /// </summary>
    public EventStoreOptionsCollection Stores { get; } = new();

    /// <summary>
    /// Gets event routing configuration.
    /// </summary>
    public EventRoutingOptions Routing { get; } = new();
}
