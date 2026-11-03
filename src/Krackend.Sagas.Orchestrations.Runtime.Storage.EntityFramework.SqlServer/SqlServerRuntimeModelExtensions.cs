using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.SqlServer;

/// <summary>SQL Server-specific model optimizations chosen by the host.</summary>
public static class SqlServerRuntimeModelExtensions
{
    /// <summary>Applies SQL Server index optimizations to the runtime model.</summary>
    /// <remarks>Apply corresponding migrations to existing databases. Call after mapping column names; the host can override the index afterward.</remarks>
    public static ModelBuilder UseKrackendSqlServerOptimizations(
        this ModelBuilder modelBuilder, Action<SqlServerRuntimeModelOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        var options = new SqlServerRuntimeModelOptions();
        configure?.Invoke(options);
        if (options.FilterStatisticsBackfillIndex)
        {
            var entity = modelBuilder.Model.FindEntityType(typeof(OrchestrationInstance))
                ?? throw new InvalidOperationException("The orchestration instance is not mapped.");
            var column = Column(entity, nameof(OrchestrationInstance.StatisticsRevision));
            modelBuilder.Entity<OrchestrationInstance>().HasIndex(x => x.StatisticsRevision)
                .HasFilter($"{column} = 0");
        }
        if (options.FilterStatisticsActivityIndex)
        {
            const string entityName = "Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure.SagaStatisticsObservation";
            var entity = modelBuilder.Model.FindEntityType(entityName)
                ?? throw new InvalidOperationException("The statistics observation is not mapped.");
            var processed = Column(entity, "Processed");
            var changed = Column(entity, "StatusChanged");
            var backfill = Column(entity, "IsBackfill");
            modelBuilder.Entity(entity.ClrType).HasIndex("OrchestrationKey", "OccurredOnUtc")
                .HasFilter($"{processed} = 1 AND {changed} = 1 AND {backfill} = 0")
                .IncludeProperties("Status");
        }
        return modelBuilder;
    }

    private static string Column(IReadOnlyEntityType entity, string propertyName)
    {
        var table = entity.GetTableName() ?? throw new InvalidOperationException($"The table for '{entity.Name}' is not mapped.");
        var column = entity.FindProperty(propertyName)?.GetColumnName(StoreObjectIdentifier.Table(table, entity.GetSchema()))
            ?? throw new InvalidOperationException($"The column for '{propertyName}' is not mapped.");
        return $"[{column.Replace("]", "]]", StringComparison.Ordinal)}]";
    }
}

/// <summary>Controls optional SQL Server optimizations.</summary>
public sealed class SqlServerRuntimeModelOptions
{
    /// <summary>Limits the statistics backfill index to instances with revision zero.</summary>
    public bool FilterStatisticsBackfillIndex { get; set; } = true;

    /// <summary>Limits the statistics activity index to processed, non-backfill status changes.</summary>
    public bool FilterStatisticsActivityIndex { get; set; }
}
