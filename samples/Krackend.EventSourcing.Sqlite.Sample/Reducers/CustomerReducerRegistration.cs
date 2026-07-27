using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Sqlite.Sample.Events;
using Krackend.EventSourcing.Sqlite.Sample.State;

namespace Krackend.EventSourcing.Sqlite.Sample.Reducers;

public static class CustomerReducerRegistration
{
    public static IEventReducerRegistry AddCustomerReducers(this IEventReducerRegistry reducers)
    {
        return reducers
            .Register<CustomerState, CustomerCreated>((state, @event) => state with
            {
                CustomerId = @event.CustomerId,
                Name = @event.Name,
                Email = @event.Email,
                IsCreated = true
            })
            .Register<CustomerState, CustomerRenamed>((state, @event) => state with
            {
                Name = @event.Name
            });
    }
}
