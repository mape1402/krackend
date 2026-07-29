using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.EventSourcing.Projections.DependencyInjection;

internal static class ProjectionAssemblyScanner
{
    public static void RegisterHandlers(IServiceCollection services, IEnumerable<Assembly> assemblies)
    {
        var types = assemblies
            .Distinct()
            .SelectMany(assembly => assembly.DefinedTypes)
            .Where(type => type is { IsAbstract: false, IsInterface: false, ContainsGenericParameters: false });

        foreach (var type in types)
        {
            foreach (var contract in type.ImplementedInterfaces.Where(IsProjectionHandler))
            {
                services.AddScoped(contract, type.AsType());
                services.AddScoped(typeof(IProjectionHandler), type.AsType());
            }
        }
    }

    private static bool IsProjectionHandler(Type contract)
        => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IProjectionHandler<>);
}
