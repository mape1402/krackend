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
        ConfigurePortableRuntimeIndexes(modelBuilder);
        if (!_memoryOptimizedExecutionState)
            ConfigureRowStoreTaskExecutionStorage(modelBuilder);
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
        entity.HasKey("Id").IsClustered(false).HasFillFactor(95);
        entity.HasIndex("StorageSequence")
            .HasDatabaseName($"CIX_{tableName}_StorageSequence")
            .IsUnique()
            .IsClustered()
            .HasFillFactor(95)
            .UseDataCompression(DataCompressionType.Row);

        foreach (var index in entity.Metadata.GetIndexes())
        {
            index.SetFillFactor(95);
            index.SetDataCompression(DataCompressionType.Row);
        }
    }

    private static void ConfigurePortableRuntimeIndexes(ModelBuilder modelBuilder)
    {
        var instances = modelBuilder.Entity<OrchestrationInstance>();

        // Extend the portable catalog index and add a separately named filtered
        // index over the same key columns for the much smaller running set.
        instances.HasIndex(x => new { x.StartedOnUtc, x.Id })
            .HasDatabaseName("IX_OrchestrationInstances_StartedOnUtc_Id")
            .IsDescending(true, true)
            .IncludeProperties(nameof(OrchestrationInstance.CorrelationId));

        instances.HasIndex(
                new[] { nameof(OrchestrationInstance.StartedOnUtc), nameof(OrchestrationInstance.Id) },
                "SqlServerRunningStartedOnUtcId")
            .HasDatabaseName("IX_OrchestrationInstances_Running_StartedOnUtc_Id")
            .HasFilter("[Status] = 'Running'")
            .IsDescending(true, true)
            .IncludeProperties(nameof(OrchestrationInstance.CorrelationId));
        instances.HasIndex(x => new { x.ExpiresOnUtc, x.Id })
            .HasDatabaseName("IX_OrchestrationInstances_Running_ExpiresOnUtc_Id")
            .HasFilter("[Status] = 'Running' AND [ExpiresOnUtc] IS NOT NULL")
            .IncludeProperties(x => x.CorrelationId);
        instances.HasIndex(x => new { x.IdleExpiresOnUtc, x.Id })
            .HasDatabaseName("IX_OrchestrationInstances_Idle_IdleExpiresOnUtc_Id")
            .HasFilter("[Status] = 'Idle' AND [IdleExpiresOnUtc] IS NOT NULL")
            .IncludeProperties(x => x.CorrelationId);
        instances.HasIndex(
                new[] { nameof(OrchestrationInstance.Id) },
                "SqlServerDispatchCancellationPendingId")
            .HasDatabaseName("IX_OrchestrationInstances_DispatchCancellationPending_Id")
            .HasFilter("[DispatchCancellationPending] = 1");

        var tasks = modelBuilder.Entity<TaskExecution>();
        tasks.HasIndex(x => new { x.ResponseDeadlineUtc, x.Id })
            .HasFilter("[Status] = 'WaitingResponse' AND [ResponseDeadlineUtc] IS NOT NULL")
            .IncludeProperties(x => x.CorrelationId);
        tasks.HasIndex(x => new { x.WaitingSinceUtc, x.Id })
            .HasFilter("[Status] = 'WaitingResponse' AND [ResponseDeadlineUtc] IS NULL")
            .IncludeProperties(x => x.CorrelationId);

        var dispatches = modelBuilder.Entity<TaskDispatch>();
        dispatches.HasIndex(x => new { x.ScheduledOnUtc, x.Id })
            .HasFilter("[DispatchStatus] = 'QueuePending'");
        dispatches.HasIndex(x => x.PhysicalCancellationPending)
            .HasFilter("[PhysicalCancellationPending] = 1");
        dispatches.HasIndex(x => new { x.CallbackPending, x.CallbackAcceptedOnUtc })
            .HasFilter("[CallbackPending] = 1");

        const string statisticsObservationName =
            "Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure.SagaStatisticsObservation";
        var statisticsObservation = modelBuilder.Model.FindEntityType(statisticsObservationName)
            ?? throw new InvalidOperationException($"Runtime entity '{statisticsObservationName}' is not mapped.");
        modelBuilder.Entity(statisticsObservation.ClrType)
            .HasIndex(new[] { "InstanceId", "Revision" }, "SqlServerPendingStatisticsObservation")
            .HasDatabaseName("IX_SagaStatisticsObservations_Pending_InstanceId_Revision")
            .HasFilter("[Processed] = 0");
    }

    private static void ConfigureRowStoreTaskExecutionStorage(ModelBuilder modelBuilder)
    {
        var tasks = modelBuilder.Entity<TaskExecution>();
        tasks.Property<byte>("StorageBucket")
            .HasComputedColumnSql(
                "CONVERT(tinyint, CONVERT(int, SUBSTRING([OrchestrationInstanceId], 16, 1)) & 63)",
                stored: true);
        tasks.HasKey(x => x.Id)
            .IsClustered(false)
            .HasFillFactor(95);
        tasks.HasIndex(
                new[] { "StorageBucket", nameof(TaskExecution.OrchestrationInstanceId), nameof(TaskExecution.Id) },
                "PortableTaskExecutionStorage")
            .HasDatabaseName("CIX_TaskExecutions_StorageBucket_OrchestrationInstanceId_Id")
            .IsUnique()
            .IsClustered()
            .HasFillFactor(95)
            .UseDataCompression(DataCompressionType.Row);
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
