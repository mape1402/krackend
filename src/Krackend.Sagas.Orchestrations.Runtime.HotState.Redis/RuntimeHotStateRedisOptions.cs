#nullable enable

namespace Krackend.Sagas.Orchestrations.Runtime.HotState.Redis;

using StackExchange.Redis;

/// <summary>Configures the Redis write-ahead runtime hot-state provider.</summary>
public sealed class RuntimeHotStateRedisOptions
{
    /// <summary>Redis connection string used when no connection factory is supplied.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Optional shared Redis connection factory.</summary>
    public Func<IServiceProvider, CancellationToken, ValueTask<IConnectionMultiplexer>>? ConnectionFactory { get; set; }

    /// <summary>Whether a connection created by the factory is owned by this provider.</summary>
    public bool DisposeConnection { get; set; }

    /// <summary>Isolates keys belonging to this runtime store.</summary>
    public string KeyPrefix { get; set; } = "krackend:runtime:hot";

    /// <summary>Redis logical database.</summary>
    public int Database { get; set; }
}
