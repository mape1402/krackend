namespace Krackend.Sagas.Orchestrations.Runtime.HotState.Redis;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers the optional Redis runtime hot-state provider.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Adds Redis as the write-ahead execution state and live read-model store.</summary>
    public static IServiceCollection AddKrackendRuntimeHotStateRedis(
        this IServiceCollection services,
        Action<RuntimeHotStateRedisOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new RuntimeHotStateRedisOptions();
        configure(options);
        if (options.ConnectionFactory is null && string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new InvalidOperationException("A Redis connection string or connection factory is required.");
        if (string.IsNullOrWhiteSpace(options.KeyPrefix))
            throw new InvalidOperationException("A non-empty Redis hot-state key prefix is required.");
        services.RemoveAll<IRuntimeHotStateStore>();
        services.AddSingleton(options);
        services.AddSingleton<IRuntimeHotStateStore, RedisRuntimeHotStateStore>();
        return services;
    }
}
