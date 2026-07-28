using Krackend.EventSourcing.Contracts;

namespace Krackend.EventSourcing.Sqlite.Sample.Events;

[EventSchema("CustomerCreated")]
public sealed record CustomerCreated(string CustomerId, string Name, string Email);
