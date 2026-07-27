namespace Krackend.EventSourcing.Sqlite.Sample.Events;

public sealed record CustomerCreated(string CustomerId, string Name, string Email);
