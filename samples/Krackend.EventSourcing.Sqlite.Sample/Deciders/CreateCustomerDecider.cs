using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Sqlite.Sample.Commands;
using Krackend.EventSourcing.Sqlite.Sample.Events;
using Krackend.EventSourcing.Sqlite.Sample.State;

namespace Krackend.EventSourcing.Sqlite.Sample.Deciders;

public sealed class CreateCustomerDecider : IEventDecider<CustomerState, CreateCustomer>
{
    public ValueTask<IReadOnlyCollection<object>> DecideAsync(
        CustomerState state,
        CreateCustomer command,
        CancellationToken cancellationToken = default)
    {
        if (state.IsCreated)
            throw new InvalidOperationException("Customer already exists.");

        return ValueTask.FromResult<IReadOnlyCollection<object>>([
            new CustomerCreated(command.CustomerId, command.Name, command.Email)
        ]);
    }
}
