using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

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
        builder.Services.Replace(
            ServiceDescriptor.Singleton<IRuntimeArtifactReadyGossipPublisher, RedisRuntimeArtifactReadyGossipPublisher>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, RedisRuntimeArtifactReadyGossipListener>());

        return builder;
    }
}
