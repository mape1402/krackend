using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis.Operations;
using Krackend.Sagas.Orchestrations.Runtime.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;

namespace Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis;

/// <summary>
/// Registers Redis gossip services for Krackend orchestration runtime.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Redis-backed runtime gossip notifications.
    /// </summary>
    /// <param name="builder">Runtime builder.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>Runtime builder.</returns>
    public static KrackendOrchestrationsRuntimeBuilder AddRedisGossip(
        this KrackendOrchestrationsRuntimeBuilder builder,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        builder.Services.PostConfigure<RuntimeGossipOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.RedisConnectionString))
            {
                options.RedisConnectionString = configuration.GetConnectionString("Redis");
            }
        });

        builder.Services.TryAddSingleton<IRedisRuntimeGossipConnectionFactory, RedisRuntimeGossipConnectionFactory>();
        builder.Services.TryAddSingleton<IRedisRuntimeReactiveConnectionFactory, RedisRuntimeReactiveConnectionFactory>();
        builder.Services.TryAddSingleton<RedisRuntimeReactiveFanout>();
        builder.Services.TryAddSingleton<IRuntimeReactiveFanout>(provider => provider.GetRequiredService<RedisRuntimeReactiveFanout>());
        builder.Services.TryAddSingleton<IRuntimeStatisticsFanout>(provider => provider.GetRequiredService<RedisRuntimeReactiveFanout>());
        builder.Services.AddHostedService(provider => provider.GetRequiredService<RedisRuntimeReactiveFanout>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IRuntimeDependencyProbe, RedisRuntimeGossipDependencyProbe>());
        builder.Services.Replace(
            ServiceDescriptor.Singleton<IRuntimeArtifactReadyGossipPublisher, RedisRuntimeArtifactReadyGossipPublisher>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, RedisRuntimeArtifactReadyGossipListener>());

        return builder;
    }
}
