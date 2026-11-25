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

    /// <summary>
    /// Creates a dedicated physical Redis connection when the supplied factory returns
    /// a shared multiplexer. Keeping hot-state payloads off the messaging connection
    /// prevents unrelated Mule traffic from introducing head-of-line blocking.
    /// </summary>
    public bool UseDedicatedConnection { get; set; } = true;

    /// <summary>Isolates keys belonging to this runtime store.</summary>
    public string KeyPrefix { get; set; } = "krackend:runtime:hot";

    /// <summary>Redis logical database.</summary>
    public int Database { get; set; }

    /// <summary>
    /// Number of independent background projection lanes. This partitions only
    /// maintenance work; foreground saga updates remain fully concurrent.
    /// </summary>
    public int AggregateLaneCount { get; set; } = 16;

    /// <summary>
    /// Maximum journal entries reserved by one durable-projection pass. Keeping this
    /// bounded prevents background HMGET/acknowledgement commands from monopolizing
    /// Redis' command thread while foreground orchestration commits are arriving.
    /// This limits maintenance batch size only; it does not limit saga concurrency.
    /// </summary>
    public int MaxProjectionLeaseSize { get; set; } = 64;

    /// <summary>
    /// Maximum journal payloads fetched by one Redis command during projection.
    /// </summary>
    public int ProjectionReadChunkSize { get; set; } = 64;

    /// <summary>
    /// Maximum terminal instances removed by one Redis cleanup command.
    /// </summary>
    public int TerminalCleanupBatchSize { get; set; } = 8;

    /// <summary>
    /// Minimum JSON payload size compressed inside Redis records. Compression is
    /// transparent to the runtime model and primarily targets repeated saga snapshots.
    /// Set to zero to disable payload compression.
    /// </summary>
    public int PayloadCompressionThresholdBytes { get; set; } = 1024;
}
