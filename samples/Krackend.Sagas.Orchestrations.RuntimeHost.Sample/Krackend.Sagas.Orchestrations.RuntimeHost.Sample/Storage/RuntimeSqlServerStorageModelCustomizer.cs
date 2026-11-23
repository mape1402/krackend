using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Storage;

/// <summary>
/// Applies SQL Server-specific storage details for the runtime sample host.
/// </summary>
public sealed class RuntimeSqlServerStorageModelCustomizer
{
    private readonly bool _memoryOptimizedExecutionState;

    /// <summary>
    /// Initializes the SQL Server model customizer.
    /// </summary>
    /// <param name="memoryOptimizedExecutionState">
    /// Whether the three hot execution-state tables use SQL Server In-Memory OLTP.
    /// </param>
    public RuntimeSqlServerStorageModelCustomizer(bool memoryOptimizedExecutionState = true)
    {
        _memoryOptimizedExecutionState = memoryOptimizedExecutionState;
    }

    /// <summary>
    /// Customizes the portable runtime storage model for SQL Server sample migrations.
    /// </summary>
    /// <param name="modelBuilder">Entity Framework model builder.</param>
    public void Configure(ModelBuilder modelBuilder)
    {
        ConfigureSqlServerIdColumns(modelBuilder);
        modelBuilder.HasSequence<long>("RuntimeStorageSequence", "Runtime")
            .StartsAt(1)
            .IncrementsBy(1);
        ConfigureRuntimeWriteStorage(modelBuilder);
        if (_memoryOptimizedExecutionState)
            ConfigureMemoryOptimizedExecutionState(modelBuilder);
        ConfigureSqlServerRuntimeDesignNodeIndexes(modelBuilder);
        modelBuilder.UseKrackendSqlServerOptimizations(options => options.FilterStatisticsActivityIndex = true);
    }

    private static void ConfigureRuntimeWriteStorage(ModelBuilder modelBuilder)
    {
        ConfigureInsertOrderedStorage<TaskExecutionAttempt>(modelBuilder, "TaskExecutionAttempts");
        ConfigureInsertOrderedStorage<TaskDispatch>(modelBuilder, "TaskDispatches");
    }

    private static void ConfigureMemoryOptimizedExecutionState(ModelBuilder modelBuilder)
    {
        var instances = modelBuilder.Entity<OrchestrationInstance>();
        instances.ToTable("OrchestrationInstances", table => table.IsMemoryOptimized());
        instances.HasKey(x => x.Id).IsClustered(false);
        foreach (var index in instances.Metadata.GetIndexes())
            index.SetFilter(null);
        // Memory-optimized tables do not support filtered indexes. Once their filters
        // are removed these indexes become a full StatisticsRevision index, a second
        // full Id index, and a duplicate of the unfiltered StartedOnUtc/Id index. The
        // equivalent reads remain covered, so these structures only amplify the log
        // on every healthy instance update.
        foreach (var index in instances.Metadata.GetIndexes()
                     .Where(index => index.GetDatabaseName() is
                         "IX_OrchestrationInstances_StatisticsRevision" or
                         "IX_OrchestrationInstances_DispatchCancellationPending_Id" or
                         "IX_OrchestrationInstances_Running_StartedOnUtc_Id")
                     .ToArray())
            instances.Metadata.RemoveIndex(index);

        var stages = modelBuilder.Entity<StageExecution>();
        stages.ToTable("StageExecutions", table => table.IsMemoryOptimized());
        stages.HasKey(x => x.Id).IsClustered(false);

        var tasks = modelBuilder.Entity<TaskExecution>();
        tasks.ToTable("TaskExecutions", table => table.IsMemoryOptimized());
        tasks.HasKey(x => x.Id).IsClustered(false);
        foreach (var index in tasks.Metadata.GetIndexes())
            index.SetFilter(null);
    }

    private static void ConfigureInsertOrderedStorage<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
    {
        var entity = modelBuilder.Entity<TEntity>();
        entity.Property<long>("StorageSequence")
            .HasDefaultValueSql("NEXT VALUE FOR [Runtime].[RuntimeStorageSequence]")
            .ValueGeneratedOnAdd();
        entity.HasKey("Id").IsClustered(false);
        entity.HasIndex("StorageSequence")
            .HasDatabaseName($"CIX_{tableName}_StorageSequence")
            .IsUnique()
            .IsClustered();
    }

    private void ConfigureSqlServerIdColumns(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(Id) || property.ClrType == typeof(Id?))
                {
                    property.SetColumnType("binary(16)");
                }
            }
        }
    }

    private void ConfigureSqlServerRuntimeDesignNodeIndexes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RuntimeDesignNode>()
            .HasIndex(x => x.InboundClientId)
            .IsUnique()
            .HasFilter("[InboundClientId] IS NOT NULL AND [InboundClientId] <> ''");
    }
}
