using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Pelican.Sample.Events;
using Krackend.EventSourcing.Pelican.Sample.State;

namespace Krackend.EventSourcing.Pelican.Sample.Reducers;

public sealed class CustomerBalanceMovedReducer : IEventReducer<CustomerState, CustomerBalanceMoved>
{
    public CustomerState Apply(CustomerState state, CustomerBalanceMoved @event)
    {
        return state with
        {
            CustomerId = @event.CustomerId,
            Balance = @event.Balance
        };
    }
}
