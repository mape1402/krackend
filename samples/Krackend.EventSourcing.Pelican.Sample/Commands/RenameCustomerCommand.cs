using Krackend.EventSourcing.Pelican.Sample.Domain;
using Krackend.EventSourcing.Streams;
using Pelican.Mediator;

namespace Krackend.EventSourcing.Pelican.Sample.Commands;

public sealed record RenameCustomerCommand(string CustomerId, string Name)
    : IRequest<CustomerResponse>, IEventStreamCommand
{
    public string StreamId => CustomerId;
}
