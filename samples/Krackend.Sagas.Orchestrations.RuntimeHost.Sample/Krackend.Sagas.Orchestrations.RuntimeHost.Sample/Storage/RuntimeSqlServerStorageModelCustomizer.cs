using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Storage;

/// <summary>
/// Applies SQL Server-specific storage details for the runtime sample host.
/// </summary>
internal sealed class RuntimeSqlServerStorageModelCustomizer
{
    /// <summary>
    /// Customizes the portable runtime storage model for SQL Server sample migrations.
    /// </summary>
    /// <param name="modelBuilder">Entity Framework model builder.</param>
    public void Configure(ModelBuilder modelBuilder)
    {
        ConfigureSqlServerIdColumns(modelBuilder);
        ConfigureSqlServerRuntimeDesignNodeIndexes(modelBuilder);
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
