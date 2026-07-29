using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Pelican.Sample.Events;
using Krackend.EventSourcing.Pelican.Sample.State;

namespace Krackend.EventSourcing.Pelican.Sample.Reducers;

public sealed class CustomerRenamedReducer : IEventReducer<CustomerState, CustomerRenamed>
{
    public CustomerState Apply(CustomerState state, CustomerRenamed @event)
    {
        return state with
        {
            CustomerId = @event.CustomerId,
            Name = @event.NormalizedName,
            LastRenameChange =
                $"v1.1 normalized '{@event.RequestedName}' to '{@event.NormalizedName}' because '{@event.Reason}'"
        };
    }
}
