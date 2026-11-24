using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.SqlServer;

/// <summary>
/// Applies the SQL Server physical model used by runtime migrations. Keeping this
/// in the provider package leaves the orchestration engine independent of SQL Server.
/// </summary>
public sealed class RuntimeSqlServerStorageModelCustomizer
{
    private readonly bool _memoryOptimizedExecutionState;

    /// <summary>Creates a SQL Server model customizer.</summary>
    /// <param name="memoryOptimizedExecutionState">
    /// Enables the optional In-Memory OLTP model for the three hot execution tables.
    /// The default row-store model works on SQL Server and Azure SQL Database without it.
    /// </param>
    public RuntimeSqlServerStorageModelCustomizer(bool memoryOptimizedExecutionState = false)
    {
        _memoryOptimizedExecutionState = memoryOptimizedExecutionState;
    }

    /// <summary>Customizes the portable runtime model for SQL Server migrations.</summary>
    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ConfigureSqlServerIdColumns(modelBuilder);
        modelBuilder.HasSequence<long>("RuntimeStorageSequence", "Runtime")
            .StartsAt(1)
            .IncrementsBy(1);
        ConfigureRuntimeWriteStorage(modelBuilder);
        if (_memoryOptimizedExecutionState)
            ConfigureMemoryOptimizedExecutionState(modelBuilder);
        ConfigureSqlServerRuntimeDesignNodeIndexes(modelBuilder);
        modelBuilder.UseKrackendSqlServerOptimizations(options =>
            options.FilterStatisticsActivityIndex = true);
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

    private static void ConfigureSqlServerIdColumns(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(Id) || property.ClrType == typeof(Id?))
                    property.SetColumnType("binary(16)");
            }
        }
    }

    private static void ConfigureSqlServerRuntimeDesignNodeIndexes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RuntimeDesignNode>()
            .HasIndex(x => x.InboundClientId)
            .IsUnique()
            .HasFilter("[InboundClientId] IS NOT NULL AND [InboundClientId] <> ''");
    }
}
