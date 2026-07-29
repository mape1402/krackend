using Krackend.EventSourcing.Contracts;

namespace Krackend.EventSourcing.Pelican.Sample.Events;

[EventSchema("CustomerBalanceMoved", "1.1.0")]
public sealed record CustomerBalanceMoved
{
    public string CustomerId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public decimal Fee { get; set; }

    public decimal BalanceAfterFee { get; set; }

    public string Description { get; set; } = string.Empty;
}
