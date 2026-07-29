using Krackend.EventSourcing.Pelican.Sample.Domain;
using Krackend.EventSourcing.Streams;
using Pelican.Mediator;

namespace Krackend.EventSourcing.Pelican.Sample.Commands;

[EventStream("customers")]
public sealed record ApplyBalanceMovementCommand(
    string CustomerId,
    decimal Amount,
    string Description)
    : IRequest<CustomerResponse>, IEventStreamCommand
{
    public string StreamId => CustomerId;
}
