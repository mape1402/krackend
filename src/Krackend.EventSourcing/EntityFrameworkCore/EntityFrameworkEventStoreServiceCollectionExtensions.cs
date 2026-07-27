using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides Entity Framework Core event store registration helpers.
/// </summary>
public static class EntityFrameworkEventStoreServiceCollectionExtensions
{
    /// <summary>
    /// Enables Krackend event sourcing using the application's Entity Framework Core DbContext.
    /// </summary>
    public static IServiceCollection AddKrackendEntityFrameworkEventStore<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKrackendEventStoreDbContextOptions<TDbContext>();
        services.AddScoped<Krackend.EventSourcing.EntityFrameworkCore.IEventStoreDbContextFactory<TDbContext>, Krackend.EventSourcing.EntityFrameworkCore.EventStoreDbContextFactory<TDbContext>>();
        services.AddScoped<Krackend.EventSourcing.EntityFrameworkCore.EntityFrameworkEventStore<TDbContext>>();
        services.Replace(ServiceDescriptor.Scoped<IEventStore>(provider => provider.GetRequiredService<Krackend.EventSourcing.EntityFrameworkCore.EntityFrameworkEventStore<TDbContext>>()));
        services.Replace(ServiceDescriptor.Scoped<IEventLogReader>(provider => provider.GetRequiredService<Krackend.EventSourcing.EntityFrameworkCore.EntityFrameworkEventStore<TDbContext>>()));

        return services;
    }

    private static IServiceCollection AddKrackendEventStoreDbContextOptions<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        var serviceType = typeof(DbContextOptions<TDbContext>);
        var descriptor = services.LastOrDefault(x => x.ServiceType == serviceType);

        if (descriptor is null)
            throw new InvalidOperationException($"DbContext '{typeof(TDbContext).Name}' must be registered before enabling the Krackend EF event store.");

        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(serviceType, provider =>
        {
            var options = ResolveOptions<TDbContext>(descriptor, provider);
            var stores = provider.GetRequiredService<EventStoreOptionsCollection>();

            return new DbContextOptionsBuilder<TDbContext>(options)
                .UseKrackendEventStoreModel(stores)
                .Options;
        }, descriptor.Lifetime));

        return services;
    }

    private static DbContextOptions<TDbContext> ResolveOptions<TDbContext>(ServiceDescriptor descriptor, IServiceProvider provider)
        where TDbContext : DbContext
    {
        if (descriptor.ImplementationInstance is DbContextOptions<TDbContext> instance)
            return instance;

        if (descriptor.ImplementationFactory is not null)
            return (DbContextOptions<TDbContext>)descriptor.ImplementationFactory(provider)!;

        if (descriptor.ImplementationType is not null)
            return (DbContextOptions<TDbContext>)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType);

        throw new InvalidOperationException($"Unable to resolve DbContextOptions for '{typeof(TDbContext).Name}'.");
    }
}
