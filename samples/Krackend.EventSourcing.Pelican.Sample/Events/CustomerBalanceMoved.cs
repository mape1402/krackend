namespace Krackend.EventSourcing.Pelican.Sample.Events;

public sealed record CustomerBalanceMoved
{
    public string CustomerId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public decimal Balance { get; set; }

    public string Description { get; set; } = string.Empty;
}
