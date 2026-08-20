using Crabalidator.DependencyInjection;
using Sales.Persistence;
using OctoMap;
using System.Diagnostics.CodeAnalysis;
using TurtlePath.Automations;
using TurtlePath.Crabalidator;
using TurtlePath.Domain.Identifier;
using TurtlePath.Mapping;
using TurtlePath.OctoMap;
using TurtlePath.Validation;
using BusinessConstants = Sales.Business.Constants;
using DomainConstants = Sales.Domain.Constants;

namespace Microsoft.Extensions.DependencyInjection
{
    [ExcludeFromCodeCoverage]
    internal static class ApplicationExtensions
    {
        internal static IServiceCollection AddApplicationDefaults(this IServiceCollection services)
        {
            services.AddPelican(typeof(BusinessConstants).Assembly);

            services.AddCrabalidator(typeof(BusinessConstants).Assembly);

            services.AddOctoMap(registration =>
            {
                registration.Options.EnableRuntimeImplicitMaps = true;
                registration.Options.DuplicateMapPolicy = DuplicateMapPolicy.Throw;
                registration.AddMaps(typeof(BusinessConstants).Assembly);
            });

            services.AddScoped<IMapperAdapter, OctoMapAdapter>();
            services.AddScoped<IValidatorAdapter, CrabalidatorAdapter>();

            services.AddTurtlePath(typeof(BusinessConstants).Assembly)
                .UseAutomations(typeof(BusinessConstants).Assembly)
                .UseOctoMap()
                .UseCrabalidator()
                .UseDataScorpio(profiles => profiles.FromAssembly(typeof(BusinessConstants).Assembly))
                // Event sourcing is ready but intentionally opt-in because it creates an append-only event store.
                // Add IEventSourcingProfile implementations in Business and uncomment this line when the service needs event streams.
                // .UseEventSourcingProfiles(typeof(Constants).Assembly)
                .UseCId<Ulid, string>(config =>
                {
                    config.DefaultFactory = () => CId.From(Ulid.NewUlid());
                    config.ConvertToDb = id => id.ToString();
                    config.ConvertFromDb = value => CId.From(Ulid.Parse(value));
                    config.JsonConverter = value => string.IsNullOrEmpty(value) ? CId.From(Ulid.Empty) : CId.From(Ulid.Parse(value));
                    config.NullableJsonConverter = value => string.IsNullOrEmpty(value) ? null : CId.From(Ulid.Parse(value));
                    config.ParseFunction = value => CId.From(Ulid.Parse(value));
                })
                .UseCIdProfiles(typeof(DomainConstants).Assembly)
                .UseEntityFrameworkCore<AppDbContext>();

            return services;
        }
    }
}
