namespace Microsoft.Extensions.DependencyInjection;

using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.Engine.DurableWork;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mule.Configuration;

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
        services.AddScoped<IRuntimeTaskDispatcher, DurableRuntimeTaskDispatcher>();
        return services;
    }

    /// <summary>
    /// Registers Krackend runtime Mule actions.
    /// </summary>
    public static IMuleRegistrationBuilder AddKrackendSagasOrchestrationsRuntimeActions(this IMuleRegistrationBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        return builder.AddActionsFromAssemblyContaining<ProcessRuntimeIngressAction>();
    }
}
