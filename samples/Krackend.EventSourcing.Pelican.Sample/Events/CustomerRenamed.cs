using Krackend.EventSourcing.Contracts;

namespace Krackend.EventSourcing.Pelican.Sample.Events;

[EventSchema("CustomerRenamed", "1.1.0")]
public sealed record CustomerRenamed
{
    public string CustomerId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}
