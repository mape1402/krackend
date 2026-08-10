using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Runtime;

/// <summary>
/// Registers runtime services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the minimal runtime module services.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Runtime options configuration.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddKrackendSagasOrchestrationsRuntime(
        this IServiceCollection services,
        Action<RuntimeModuleOptions> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var options = new RuntimeModuleOptions();
        configure(options);

        if (string.IsNullOrWhiteSpace(options.EnvironmentKey))
        {
            throw new InvalidOperationException("Runtime environment key is required.");
        }

        services.AddSingleton(new RuntimeEnvironmentDescriptor(options.EnvironmentKey.Trim()));

        return services;
    }
}
