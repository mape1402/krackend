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
}
