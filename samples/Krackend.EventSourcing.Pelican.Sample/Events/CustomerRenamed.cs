using Krackend.EventSourcing.Contracts;

namespace Krackend.EventSourcing.Pelican.Sample.Events;

[EventSchemaVersion("1.0.0")]
public sealed record CustomerRenamed
{
    public string CustomerId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
