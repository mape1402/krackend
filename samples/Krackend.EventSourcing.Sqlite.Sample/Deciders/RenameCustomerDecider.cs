using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Sqlite.Sample.Commands;
using Krackend.EventSourcing.Sqlite.Sample.Events;
using Krackend.EventSourcing.Sqlite.Sample.State;

namespace Krackend.EventSourcing.Sqlite.Sample.Deciders;

public sealed class RenameCustomerDecider : IEventDecider<CustomerState, RenameCustomer>
{
    public ValueTask<IReadOnlyCollection<object>> DecideAsync(
        CustomerState state,
        RenameCustomer command,
        CancellationToken cancellationToken = default)
    {
        if (!state.IsCreated)
            throw new InvalidOperationException("Customer must exist before it can be renamed.");

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ArgumentException("Customer name is required.", nameof(command));

        return ValueTask.FromResult<IReadOnlyCollection<object>>([
            new CustomerRenamed(command.CustomerId, command.Name)
        ]);
    }
}
