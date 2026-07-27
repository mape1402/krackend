using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Pelican.Sample.Events;
using Krackend.EventSourcing.Pelican.Sample.State;

namespace Krackend.EventSourcing.Pelican.Sample.Reducers;

public sealed class CustomerCreatedReducer : IEventReducer<CustomerState, CustomerCreated>
{
    public CustomerState Apply(CustomerState state, CustomerCreated @event)
    {
        return state with
        {
            CustomerId = @event.CustomerId,
            Name = @event.Name,
            Email = @event.Email,
            IsCreated = true
        };
    }
}
