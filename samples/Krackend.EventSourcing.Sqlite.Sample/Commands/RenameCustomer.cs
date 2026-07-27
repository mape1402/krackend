using Krackend.EventSourcing.Streams;

namespace Krackend.EventSourcing.Sqlite.Sample.Commands;

public sealed record RenameCustomer(string CustomerId, string Name) : IEventStreamCommand
{
    public string StreamId => CustomerId;
}
