namespace Krackend.EventSourcing.Pelican.Sample.Events;

public sealed record CustomerRenamed
{
    public string CustomerId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
