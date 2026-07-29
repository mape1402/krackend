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
            Balance = @event.BalanceAfterFee,
            LastBalanceChange =
                $"v1.1 applied amount {@event.Amount} minus fee {@event.Fee} => {@event.BalanceAfterFee}"
        };
    }
}
