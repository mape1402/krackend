using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Pelican.Sample.Events;
using Krackend.EventSourcing.Pelican.Sample.State;

namespace Krackend.EventSourcing.Pelican.Sample.Reducers;

public sealed class CustomerBalanceMovedV1Reducer : IEventReducer<CustomerState, CustomerBalanceMovedV1>
{
    public CustomerState Apply(CustomerState state, CustomerBalanceMovedV1 @event)
    {
        return state with
        {
            CustomerId = @event.CustomerId,
            Balance = @event.Balance,
            LastBalanceChange = $"v1 applied amount {@event.Amount} directly => {@event.Balance}"
        };
    }
}
