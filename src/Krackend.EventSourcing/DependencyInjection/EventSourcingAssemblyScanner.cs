using System.Reflection;
using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Projections;
using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Streams;
using Krackend.EventSourcing.Upcasting;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.EventSourcing.DependencyInjection;

internal static class EventSourcingAssemblyScanner
{
    public static void RegisterComponents(
        IServiceCollection services,
        EventTypeRegistry eventTypeRegistry,
        IEnumerable<Assembly> assemblies)
    {
        var types = assemblies
            .Distinct()
            .SelectMany(assembly => assembly.DefinedTypes)
            .Where(type => type is { IsAbstract: false, IsInterface: false, ContainsGenericParameters: false })
            .ToArray();

        foreach (var type in types)
        {
            RegisterClosedInterfaces(services, type, typeof(IEventDecider<,>), ServiceLifetime.Scoped);
            RegisterClosedInterfaces(services, type, typeof(IProjectionHandler<>), ServiceLifetime.Scoped);
            RegisterClosedInterfaces(services, type, typeof(IEventReducer<,>), ServiceLifetime.Scoped);
            RegisterClosedInterfaces(services, type, typeof(ICommandStreamResolver<>), ServiceLifetime.Scoped);
            RegisterAssignable(services, type, typeof(IEventUpcaster), ServiceLifetime.Singleton);
            RegisterEventTypes(eventTypeRegistry, type);
        }
    }

    private static void RegisterAssignable(
        IServiceCollection services,
        TypeInfo implementationType,
        Type contract,
        ServiceLifetime lifetime)
    {
        if (!contract.IsAssignableFrom(implementationType.AsType()))
            return;

        services.Add(new ServiceDescriptor(contract, implementationType.AsType(), lifetime));
    }

    public static void RegisterReducers(IServiceProvider provider, IEventReducerRegistry registry)
    {
        foreach (var reducer in provider.GetServices<IEventReducer>())
            registry.Register(reducer);
    }

    private static void RegisterClosedInterfaces(
        IServiceCollection services,
        TypeInfo implementationType,
        Type openGenericContract,
        ServiceLifetime lifetime)
    {
        foreach (var contract in implementationType.ImplementedInterfaces.Where(x =>
            x.IsGenericType && x.GetGenericTypeDefinition() == openGenericContract))
        {
            services.Add(new ServiceDescriptor(contract, implementationType.AsType(), lifetime));

            if (openGenericContract == typeof(IEventReducer<,>))
                services.Add(new ServiceDescriptor(typeof(IEventReducer), implementationType.AsType(), lifetime));

            if (openGenericContract == typeof(IProjectionHandler<>))
                services.Add(new ServiceDescriptor(typeof(IProjectionHandler), implementationType.AsType(), lifetime));
        }
    }

    private static void RegisterEventTypes(EventTypeRegistry registry, TypeInfo implementationType)
    {
        foreach (var contract in implementationType.ImplementedInterfaces.Where(IsEventContract))
        {
            var eventType = contract.GetGenericTypeDefinition() == typeof(IEventReducer<,>)
                ? contract.GetGenericArguments()[1]
                : contract.GetGenericArguments()[0];

            registry.Register(eventType);
        }
    }

    private static bool IsEventContract(Type contract)
    {
        if (!contract.IsGenericType)
            return false;

        var definition = contract.GetGenericTypeDefinition();
        return definition == typeof(IEventReducer<,>) || definition == typeof(IProjectionHandler<>);
    }
}
