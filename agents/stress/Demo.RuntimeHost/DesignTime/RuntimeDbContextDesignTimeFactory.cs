using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;

namespace Krackend.Demo.RuntimeHost.DesignTime;

public sealed class RuntimeDbContextDesignTimeFactory : IDesignTimeDbContextFactory<RuntimeDbContext>
{
    public RuntimeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("KRACKEND_DEMO_MIGRATIONS_SQLSERVER")
            ?? "Server=localhost;Database=KrackendRuntime;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<RuntimeDbContext>()
            .UseSqlServer(connectionString, sqlServer =>
            {
                sqlServer.MigrationsAssembly(typeof(RuntimeDbContextDesignTimeFactory).Assembly.GetName().Name);
                sqlServer.MigrationsHistoryTable("__RuntimeStorageMigrationsHistory", "Runtime");
            })
            .Options;
        var configuredStorage = new RuntimeEntityFrameworkStorageOptions
        {
            PersistRoutineExecutionTransitions = false,
            PersistSuccessfulTransportOutcomes = false,
            EmbedCurrentTransportState = true,
            CompressSnapshotPayload = true,
            PersistStatisticsObservations = false
        };
        configuredStorage.UseKrackendSqlServerRuntime();
        var storageOptions = Options.Create(configuredStorage);
        return RuntimeDbContext.Create(options, storageOptions);
    }
}
