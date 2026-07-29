using System.Reflection;
using Krackend.EventSourcing.Contracts;
using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Streams;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.EventSourcing.DependencyInjection;

internal static class EventSourcingAssemblyScanner
{
    internal static void RegisterComponents(
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
            RegisterClosedInterfaces(services, type, typeof(IEventReducer<,>), ServiceLifetime.Scoped);
            RegisterClosedInterfaces(services, type, typeof(ICommandStreamResolver<>), ServiceLifetime.Scoped);
            RegisterClosedInterfaces(services, type, typeof(IInitialStateFactory<>), ServiceLifetime.Scoped);
            RegisterEventSchema(eventTypeRegistry, type);
        }
    }

    internal static void RegisterReducers(IServiceProvider provider, IEventReducerRegistry registry)
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
        }
    }

    private static void RegisterEventSchema(EventTypeRegistry registry, TypeInfo implementationType)
    {
        if (implementationType.GetCustomAttribute<EventSchemaAttribute>() is not null)
            registry.Register(implementationType.AsType());
    }
}
