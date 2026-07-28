using Krackend.EventSourcing.Contracts;

namespace Krackend.EventSourcing.Sqlite.Sample.Events;

[EventSchema("CustomerRenamed")]
public sealed record CustomerRenamed(string CustomerId, string Name);
