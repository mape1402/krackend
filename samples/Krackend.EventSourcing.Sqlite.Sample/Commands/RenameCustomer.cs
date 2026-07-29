using Krackend.EventSourcing.Streams;

namespace Krackend.EventSourcing.Sqlite.Sample.Commands;

[EventStream("customers")]
public sealed record RenameCustomer(string CustomerId, string Name) : IEventStreamCommand
{
    public string StreamId => CustomerId;
}
