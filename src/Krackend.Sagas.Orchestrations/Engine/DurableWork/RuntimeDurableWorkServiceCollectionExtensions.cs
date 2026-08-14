namespace Microsoft.Extensions.DependencyInjection;

using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.Engine.DurableWork;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registers durable work services for the orchestration runtime.
/// </summary>
public static class RuntimeDurableWorkServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Mule-backed durable work scheduler used by the runtime inbox/outbox pipeline.
    /// </summary>
    public static IServiceCollection AddKrackendSagasOrchestrationsMuleDurableWork(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        services.TryAddScoped<IRuntimeDurableWorkScheduler, MuleRuntimeDurableWorkScheduler>();
        return services;
    }
}
