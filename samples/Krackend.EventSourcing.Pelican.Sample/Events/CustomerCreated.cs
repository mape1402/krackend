namespace Krackend.EventSourcing.Pelican.Sample.Events;

public sealed record CustomerCreated(string CustomerId, string Name, string Email);
