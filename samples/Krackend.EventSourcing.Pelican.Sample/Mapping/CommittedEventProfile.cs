using Krackend.EventSourcing.Pelican.Sample.Commands;
using Krackend.EventSourcing.Pelican.Sample.Domain;
using Krackend.EventSourcing.Pelican.Sample.Events;
using OctoMap;

namespace Krackend.EventSourcing.Pelican.Sample.Mapping;

public sealed class CommittedEventProfile : OctoMapProfile
{
    public override void Configure(IOctoMapConfigurationBuilder builder)
    {
        builder.CreateMultiMap<CustomerCreated>()
            .From<CreateCustomerCommand>(_ => { })
            .From<Customer>(map => map
                .ForMember(x => x.CustomerId, x => x.MapFrom(s => s.Id))
                .ForMember(x => x.Name, x => x.MapFrom(s => s.Name))
                .ForMember(x => x.Email, x => x.MapFrom(s => s.Email)));
    }
}
