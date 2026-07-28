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

        builder.CreateMultiMap<CustomerRenamed>()
            .From<RenameCustomerCommand>(_ => { })
            .From<Customer>(map => map
                .ForMember(x => x.CustomerId, x => x.MapFrom(s => s.Id))
                .ForMember(x => x.Name, x => x.MapFrom(s => s.Name)));

        builder.CreateMultiMap<CustomerBalanceMoved>()
            .From<ApplyBalanceMovementCommand>(map => map
                .ForMember(x => x.Amount, x => x.MapFrom(s => s.Amount))
                .ForMember(x => x.Description, x => x.MapFrom(s => s.Description)))
            .From<Customer>(map => map
                .ForMember(x => x.CustomerId, x => x.MapFrom(s => s.Id))
                .ForMember(x => x.Balance, x => x.MapFrom(s => s.Balance)));

        builder.CreateMultiMap<CustomerBalanceMovedV1>()
            .From<ApplyLegacyBalanceMovementCommand>(map => map
                .ForMember(x => x.Amount, x => x.MapFrom(s => s.Amount)))
            .From<Customer>(map => map
                .ForMember(x => x.CustomerId, x => x.MapFrom(s => s.Id))
                .ForMember(x => x.Balance, x => x.MapFrom(s => s.Balance)));
    }
}
