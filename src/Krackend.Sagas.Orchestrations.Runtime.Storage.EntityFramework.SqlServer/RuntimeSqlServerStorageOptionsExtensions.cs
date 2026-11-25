using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.SqlServer;

/// <summary>Configures the runtime adapter with the SQL Server provider package.</summary>
public static class RuntimeSqlServerStorageOptionsExtensions
{
    /// <summary>
    /// Adds SQL Server physical mappings and runtime operations. Calling this from
    /// both runtime and design-time configuration makes host-owned EF migrations
    /// include every required index and storage structure.
    /// </summary>
    public static RuntimeEntityFrameworkStorageOptions UseKrackendSqlServerRuntime(
        this RuntimeEntityFrameworkStorageOptions storage,
        Action<RuntimeSqlServerOptimizationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(storage);
        var options = new RuntimeSqlServerOptimizationOptions();
        configure?.Invoke(options);
        if (options.DurableLogFlushWindow < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options.DurableLogFlushWindow));
        if (options.DurableLogFlushConcurrency < 1)
            throw new ArgumentOutOfRangeException(nameof(options.DurableLogFlushConcurrency));

        var existingModelConfiguration = storage.ConfigureModel;
        var modelCustomizer = new RuntimeSqlServerStorageModelCustomizer(
            options.MemoryOptimizedExecutionState);
        storage.ConfigureModel = modelBuilder =>
        {
            modelCustomizer.Configure(modelBuilder);
            existingModelConfiguration?.Invoke(modelBuilder);
        };

        var provider = new RuntimeSqlServerProviderOptimizations(options);
        storage.ProviderOptimizations = new RuntimeEntityFrameworkProviderOptimizations
        {
            TryAcquireTransactionLockAsync = provider.TryAcquireTransactionLockAsync,
            TryReadCallbackGraphAsync = provider.TryReadCallbackGraphAsync,
            AwaitDurabilityAsync = provider.AwaitDurabilityAsync
        };
        return storage;
    }
}
