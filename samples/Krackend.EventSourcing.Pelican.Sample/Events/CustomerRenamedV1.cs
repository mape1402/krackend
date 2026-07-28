using Krackend.EventSourcing.Contracts;

namespace Krackend.EventSourcing.Pelican.Sample.Events;

[EventSchema("CustomerRenamed", "1.0.0")]
public sealed record CustomerRenamedV1
{
    public string CustomerId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
