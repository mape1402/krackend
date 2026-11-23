using System.Diagnostics.Metrics;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

/// <summary>Low-cardinality measurements of runtime acceptance and coordination.</summary>
public static class RuntimeConcurrencyMetrics
{
    /// <summary>Meter to subscribe to from OpenTelemetry or a local diagnostic listener.</summary>
    public static readonly Meter Meter = new("Krackend.Runtime.Concurrency", "1.0.0");
    /// <summary>Time spent accepting durable ingress work.</summary>
    public static readonly Histogram<double> IntakeMilliseconds = Meter.CreateHistogram<double>("krackend.intake.duration", "ms");
    /// <summary>Time spent acquiring an instance lease.</summary>
    public static readonly Histogram<double> LeaseAcquisitionMilliseconds = Meter.CreateHistogram<double>("krackend.lease.acquire.duration", "ms");
    /// <summary>Time spent executing the protected mutation.</summary>
    public static readonly Histogram<double> MutationMilliseconds = Meter.CreateHistogram<double>("krackend.mutation.duration", "ms");
    /// <summary>Time spent publishing to external services.</summary>
    public static readonly Histogram<double> PublicationMilliseconds = Meter.CreateHistogram<double>("krackend.publication.duration", "ms");
    /// <summary>Time spent evaluating the next orchestration decision set.</summary>
    public static readonly Histogram<double> DecisionMilliseconds = Meter.CreateHistogram<double>("krackend.decision.duration", "ms");
    /// <summary>Time spent applying one orchestration decision set.</summary>
    public static readonly Histogram<double> DecisionExecutionMilliseconds = Meter.CreateHistogram<double>("krackend.decision.execute.duration", "ms");
    /// <summary>Time spent flushing a runtime state unit of work.</summary>
    public static readonly Histogram<double> StorageSaveMilliseconds = Meter.CreateHistogram<double>("krackend.storage.save.duration", "ms");
    /// <summary>Time spent detecting tracked runtime state changes before a flush.</summary>
    public static readonly Histogram<double> StorageDetectChangesMilliseconds = Meter.CreateHistogram<double>("krackend.storage.detect_changes.duration", "ms");
    /// <summary>Time spent committing a runtime state transaction.</summary>
    public static readonly Histogram<double> StorageCommitMilliseconds = Meter.CreateHistogram<double>("krackend.storage.commit.duration", "ms");
    /// <summary>Time spent making grouped SQL Server commits physically durable.</summary>
    public static readonly Histogram<double> StorageDurableLogFlushMilliseconds = Meter.CreateHistogram<double>("krackend.storage.durable_log_flush.duration", "ms");
    /// <summary>End-to-end time each transaction waits at the durable log barrier.</summary>
    public static readonly Histogram<double> StorageDurableLogBarrierMilliseconds = Meter.CreateHistogram<double>("krackend.storage.durable_log_barrier.duration", "ms");
    /// <summary>Number of committed transactions made durable by one physical log flush.</summary>
    public static readonly Histogram<long> StorageDurableLogFlushBatchSize = Meter.CreateHistogram<long>("krackend.storage.durable_log_flush.batch_size", "transactions");
    /// <summary>Number of distinct callbacks applied by one instance-local engine activation.</summary>
    public static readonly Histogram<long> CallbackBatchSize = Meter.CreateHistogram<long>("krackend.callback.batch.size", "callbacks");
    /// <summary>Callbacks that reused the last committed single-replica execution state.</summary>
    public static readonly Counter<long> CallbackStateCacheHits = Meter.CreateCounter<long>("krackend.callback.state_cache.hit");
    /// <summary>Callbacks that loaded their execution state from durable storage.</summary>
    public static readonly Counter<long> CallbackStateCacheMisses = Meter.CreateCounter<long>("krackend.callback.state_cache.miss");
    /// <summary>Durable work deferred without consuming a functional attempt.</summary>
    public static readonly Counter<long> Deferred = Meter.CreateCounter<long>("krackend.actions.deferred");
    /// <summary>Lease attempts that found another active operation.</summary>
    public static readonly Counter<long> LeaseContention = Meter.CreateCounter<long>("krackend.lease.contention");
}
