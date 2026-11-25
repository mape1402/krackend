namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.SqlServer;

/// <summary>Configures SQL Server-specific runtime optimizations.</summary>
public sealed class RuntimeSqlServerOptimizationOptions
{
    /// <summary>Uses transaction-owned application locks for per-saga fencing.</summary>
    public bool UseTransactionApplicationLocks { get; set; } = true;

    /// <summary>Uses the compact single-round-trip callback graph reader.</summary>
    public bool UseCompactCallbackGraphReader { get; set; } = true;

    /// <summary>
    /// Coordinates explicit physical log barriers for databases configured with
    /// forced delayed durability.
    /// </summary>
    public bool CoordinateDurableLogFlushes { get; set; }

    /// <summary>Gets or sets the collection window for already-committed transactions.</summary>
    public TimeSpan DurableLogFlushWindow { get; set; } = TimeSpan.FromMilliseconds(4);

    /// <summary>Gets or sets the maximum concurrent physical log flush commands.</summary>
    public int DurableLogFlushConcurrency { get; set; } = 4;

    /// <summary>Enables the optional SQL Server In-Memory OLTP execution model.</summary>
    public bool MemoryOptimizedExecutionState { get; set; }
}
