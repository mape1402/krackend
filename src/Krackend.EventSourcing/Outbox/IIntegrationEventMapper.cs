namespace Krackend.EventSourcing.Outbox;

/// <summary>
/// Maps a domain event into an integration event.
/// </summary>
public interface IIntegrationEventMapper<in TDomainEvent>
{
    /// <summary>
    /// Maps a domain event to an integration event or returns null when nothing should be published.
    /// </summary>
    object? Map(TDomainEvent domainEvent);
}
