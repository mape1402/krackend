using Krackend.EventSourcing.Streams;

namespace Krackend.EventSourcing.Sqlite.Sample.Commands;

[EventStream("customers")]
public sealed record CreateCustomer(string CustomerId, string Name, string Email) : IEventStreamCommand
{
    public string StreamId => CustomerId;
}
