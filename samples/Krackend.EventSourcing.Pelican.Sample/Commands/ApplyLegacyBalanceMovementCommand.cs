using Krackend.EventSourcing.Pelican.Sample.Domain;
using Krackend.EventSourcing.Streams;
using Pelican.Mediator;

namespace Krackend.EventSourcing.Pelican.Sample.Commands;

public sealed record ApplyLegacyBalanceMovementCommand(string CustomerId, decimal Amount)
    : IRequest<CustomerResponse>, IEventStreamCommand
{
    public string StreamId => CustomerId;
}
