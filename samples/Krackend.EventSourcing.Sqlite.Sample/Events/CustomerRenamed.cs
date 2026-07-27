namespace Krackend.EventSourcing.Sqlite.Sample.Events;

public sealed record CustomerRenamed(string CustomerId, string Name);
