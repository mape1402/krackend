using Krackend.EventSourcing.Pelican.Sample.Commands;
using Krackend.EventSourcing.Pelican.Sample.Domain;
using Krackend.EventSourcing.Pelican.Sample.Events;

namespace Krackend.EventSourcing.Pelican.Sample.Hooks;

public sealed class CustomerCreatedEventFactory : ICommittedEventFactory<CreateCustomerCommand, Customer>
{
    public ValueTask<object?> CreateAsync(
        CreateCustomerCommand request,
        Customer entity,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<object?>(new CustomerCreated(entity.Id, entity.Name, entity.Email));
    }
}
