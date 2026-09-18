using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Storage;

/// <summary>
/// Applies SQL Server-specific storage details for the control-plane sample host.
/// </summary>
internal sealed class ControlPlaneSqlServerStorageModelCustomizer
{
    /// <summary>
    /// Customizes the portable control-plane storage model for SQL Server sample migrations.
    /// </summary>
    /// <param name="modelBuilder">Entity Framework model builder.</param>
    public void Configure(ModelBuilder modelBuilder)
    {
        ConfigureSqlServerIdColumns(modelBuilder);
        ConfigureSqlServerRuntimeNodeIndexes(modelBuilder);
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

    private void ConfigureSqlServerRuntimeNodeIndexes(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<RuntimeNodeEntity>();

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(x => x.InboundClientId)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [InboundClientId] IS NOT NULL AND [InboundClientId] <> ''");
    }
}
