using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Sqlite.Sample.Events;
using Krackend.EventSourcing.Sqlite.Sample.State;

namespace Krackend.EventSourcing.Sqlite.Sample.Reducers;

public sealed class CustomerRenamedReducer : IEventReducer<CustomerState, CustomerRenamed>
{
    public CustomerState Apply(CustomerState state, CustomerRenamed @event)
    {
        return state with
        {
            Name = @event.Name
        };
    }
}
