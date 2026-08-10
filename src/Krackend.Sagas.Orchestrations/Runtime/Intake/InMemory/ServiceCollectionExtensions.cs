using Microsoft.Extensions.DependencyInjection;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;

namespace Krackend.Sagas.Orchestrations.Runtime.Intake.InMemory;

/// <summary>
/// Registers the in-memory runtime intake buffer.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the in-memory runtime intake buffer.
    /// </summary>
    public static IServiceCollection AddKrackendSagasOrchestrationsInMemoryIntakeBuffer(
        this IServiceCollection services,
        Action<InMemoryTriggerIntakeBufferOptions>? configure = null)
    {
        var options = new InMemoryTriggerIntakeBufferOptions();
        configure?.Invoke(options);

        if (options.Capacity <= 0)
        {
            throw new InvalidOperationException("In-memory trigger intake buffer capacity must be greater than zero.");
        }

        services.AddSingleton(options);
        services.AddSingleton<ITriggerIntakeBuffer, InMemoryTriggerIntakeBuffer>();
        return services;
    }
}
