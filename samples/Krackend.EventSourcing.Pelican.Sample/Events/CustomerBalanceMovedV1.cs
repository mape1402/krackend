using Krackend.EventSourcing.Contracts;

namespace Krackend.EventSourcing.Pelican.Sample.Events;

[EventType("CustomerBalanceMoved")]
[EventSchemaVersion("1.0.0")]
public sealed record CustomerBalanceMovedV1
{
    public string CustomerId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public decimal Balance { get; set; }
}
