using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Pelican.Sample.Events;
using Krackend.EventSourcing.Pelican.Sample.State;

namespace Krackend.EventSourcing.Pelican.Sample.Reducers;

public sealed class CustomerRenamedV1Reducer : IEventReducer<CustomerState, CustomerRenamedV1>
{
    public CustomerState Apply(CustomerState state, CustomerRenamedV1 @event)
    {
        return state with
        {
            CustomerId = @event.CustomerId,
            Name = @event.Name,
            LastRenameChange = $"v1 kept raw name '{@event.Name}'"
        };
    }
}
