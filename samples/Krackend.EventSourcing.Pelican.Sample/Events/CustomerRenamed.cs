using Krackend.EventSourcing.Contracts;

namespace Krackend.EventSourcing.Pelican.Sample.Events;

[EventSchema("CustomerRenamed")]
public sealed record CustomerRenamed
{
    public string CustomerId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
