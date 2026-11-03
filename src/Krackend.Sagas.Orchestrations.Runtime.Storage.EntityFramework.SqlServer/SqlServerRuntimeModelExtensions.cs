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
        if (!options.FilterStatisticsBackfillIndex) return modelBuilder;

        var entity = modelBuilder.Model.FindEntityType(typeof(OrchestrationInstance))
            ?? throw new InvalidOperationException("The orchestration instance is not mapped.");
        var table = entity.GetTableName() ?? throw new InvalidOperationException("The orchestration instance table is not mapped.");
        var column = entity.FindProperty(nameof(OrchestrationInstance.StatisticsRevision))
            ?.GetColumnName(StoreObjectIdentifier.Table(table, entity.GetSchema()))
            ?? throw new InvalidOperationException("The statistics revision column is not mapped.");
        modelBuilder.Entity<OrchestrationInstance>().HasIndex(x => x.StatisticsRevision)
            .HasFilter($"[{column.Replace("]", "]]", StringComparison.Ordinal)}] = 0");
        return modelBuilder;
    }
}

/// <summary>Controls optional SQL Server optimizations.</summary>
public sealed class SqlServerRuntimeModelOptions
{
    /// <summary>Limits the statistics backfill index to instances with revision zero.</summary>
    public bool FilterStatisticsBackfillIndex { get; set; } = true;
}
