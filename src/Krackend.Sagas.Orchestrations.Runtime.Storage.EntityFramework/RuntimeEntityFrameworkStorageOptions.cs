#nullable enable

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;

/// <summary>
/// Provides host-level customization options for the runtime Entity Framework storage adapter.
/// </summary>
public sealed class RuntimeEntityFrameworkStorageOptions
{
    /// <summary>
    /// Gets or sets whether routine successful lifecycle transitions are retained in
    /// the execution-transition history. Reactive events are still published and
    /// failure, retry, timeout, compensation and recovery transitions remain durable.
    /// Disable this for high-throughput runtimes whose functional state tables are the
    /// source of truth and which do not require a verbose success audit trail.
    /// </summary>
    public bool PersistRoutineExecutionTransitions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether successful callback outcomes are copied synchronously to
    /// the attempt and dispatch audit rows. The task execution remains the functional
    /// source of truth either way; failures and durable callback receipts are always
    /// persisted on every layer. Disable this for high-throughput runtimes to avoid
    /// writing the same successful terminal state three times.
    /// </summary>
    public bool PersistSuccessfulTransportOutcomes { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the current attempt and durable dispatch intent are
    /// embedded in the canonical task row instead of inserted as two additional
    /// hot-path rows. Failure and recovery state remains durable.
    /// </summary>
    public bool EmbedCurrentTransportState { get; set; }

    /// <summary>
    /// Gets or sets whether the opaque orchestration snapshot is compressed in the
    /// database. Existing plain JSON snapshots remain readable. Enable this for
    /// payload-heavy runtimes to avoid rewriting large LOB values on every callback.
    /// </summary>
    public bool CompressSnapshotPayload { get; set; }

    /// <summary>
    /// Gets or sets whether status observations used by the statistics projection are
    /// inserted in the functional saga transaction.
    /// </summary>
    public bool PersistStatisticsObservations { get; set; } = true;

    /// <summary>
    /// Gets or sets how long the durable hot-state projector coalesces journal entries.
    /// Live reads continue to use the Redis overlay during this bounded interval.
    /// </summary>
    public TimeSpan HotStateProjectionInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Gets or sets the longest time an uninterrupted foreground burst may defer a
    /// durable projection. Once reached, the projector drains a batch even while new
    /// mutations continue to arrive.
    /// </summary>
    public TimeSpan HotStateProjectionMaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the maximum journal batches collapsed into one durable projection.</summary>
    public int HotStateProjectionBatchSize { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets whether SQL Server commits are completed with a shared explicit
    /// log flush. This option requires the database to use forced delayed durability.
    /// Every caller still waits for the physical log flush before work is acknowledged;
    /// concurrent transactions merely share that flush instead of issuing one each.
    /// </summary>
    public bool CoordinateSqlServerDurableLogFlushes { get; set; }

    /// <summary>
    /// Gets or sets the short collection window used to group already-committed SQL
    /// Server transactions into one durable log flush.
    /// </summary>
    public TimeSpan SqlServerDurableLogFlushWindow { get; set; } = TimeSpan.FromMilliseconds(4);

    /// <summary>
    /// Gets or sets the maximum number of physical SQL Server log flush commands that
    /// may be in flight for one database. This does not limit saga or transaction
    /// concurrency; commits waiting for a physical slot coalesce behind one barrier.
    /// </summary>
    public int SqlServerDurableLogFlushConcurrency { get; set; } = 4;

    /// <summary>
    /// Gets or sets a value indicating whether Data Protection keys used for distribution secrets are stored in the runtime database.
    /// </summary>
    public bool PersistDataProtectionKeysToStorage { get; set; } = true;

    /// <summary>
    /// Gets or sets the stable Data Protection application name shared by all runtime replicas.
    /// </summary>
    public string DataProtectionApplicationName { get; set; } = "Krackend.Sagas.Orchestrations.Runtime";

    /// <summary>
    /// Gets or sets an optional callback that can replace or harden the Data Protection key storage configured by this adapter.
    /// </summary>
    public Action<IDataProtectionBuilder>? ConfigureDataProtection { get; set; }

    /// <summary>
    /// Gets or sets an optional callback that customizes the Entity Framework model after the portable default mappings are applied.
    /// </summary>
    public Action<ModelBuilder>? ConfigureModel { get; set; }
}
