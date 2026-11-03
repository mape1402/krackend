using DotNet.Testcontainers.Builders;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.SqlServer;
using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage;
using Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class SqlServerStatisticsBackfillIndexTests
{
    private const string PreviousMigration = "20261101010000_AddInstanceProgressProjection";
    private const string Password = "Krackend_12345!";

    [Fact]
    public void FilteringIsConfiguredByTheSqlHostOnly()
    {
        using var sqlite = CreateProvider(options => options.UseSqlite("Data Source=:memory:"));
        using var sql = CreateProvider(options => options.UseSqlServer("Server=localhost;Database=unused;User Id=sa;Password=unused;TrustServerCertificate=True"),
            new RuntimeSqlServerStorageModelCustomizer());
        using var sqliteScope = sqlite.CreateScope();
        using var sqlScope = sql.CreateScope();
        var sqliteIndex = RevisionIndex(sqliteScope.ServiceProvider.GetRequiredService<RuntimeDbContext>());
        var sqlIndex = RevisionIndex(sqlScope.ServiceProvider.GetRequiredService<RuntimeDbContext>());
        Assert.Null(sqliteIndex.GetFilter());
        Assert.Equal("[StatisticsRevision] = 0", sqlIndex.GetFilter());
    }

    [Fact]
    public void SqlServerDefaultsCanBeDisabledOrOverriddenByTheHost()
    {
        using var recommended = CreateSqlProvider(model => model.UseKrackendSqlServerOptimizations());
        using var disabled = CreateSqlProvider(model => model.UseKrackendSqlServerOptimizations(
            options => options.FilterStatisticsBackfillIndex = false));
        using var overridden = CreateSqlProvider(model =>
        {
            model.UseKrackendSqlServerOptimizations();
            model.Entity<OrchestrationInstance>().HasIndex(x => x.StatisticsRevision).HasFilter(null);
        });
        using var renamed = CreateSqlProvider(model =>
        {
            model.Entity<OrchestrationInstance>().Property(x => x.StatisticsRevision).HasColumnName("Revision]Sequence");
            model.UseKrackendSqlServerOptimizations();
        });
        using var recommendedScope = recommended.CreateScope();
        using var disabledScope = disabled.CreateScope();
        using var overriddenScope = overridden.CreateScope();
        using var renamedScope = renamed.CreateScope();
        Assert.Equal("[StatisticsRevision] = 0", RevisionIndex(recommendedScope.ServiceProvider.GetRequiredService<RuntimeDbContext>()).GetFilter());
        Assert.Null(RevisionIndex(disabledScope.ServiceProvider.GetRequiredService<RuntimeDbContext>()).GetFilter());
        Assert.Null(RevisionIndex(overriddenScope.ServiceProvider.GetRequiredService<RuntimeDbContext>()).GetFilter());
        Assert.Equal("[Revision]]Sequence] = 0", RevisionIndex(renamedScope.ServiceProvider.GetRequiredService<RuntimeDbContext>()).GetFilter());
    }

    [Fact(Timeout = 180_000)]
    public async Task MigrationAndRollbackPreserveBackfillAndConcurrencyOnSqlServer()
    {
        await using var container = new ContainerBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("MSSQL_SA_PASSWORD", Password)
            .WithPortBinding(1433, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(1433))
            .Build();
        await container.StartAsync();
        var port = container.GetMappedPublicPort(1433);
        var master = $"Server=localhost,{port};Database=master;User Id=sa;Password={Password};TrustServerCertificate=True;Encrypt=False;Connect Timeout=2";
        await WaitForSqlServer(master);
        var connection = $"Server=localhost,{port};Database=KrackendBackfillIndex;User Id=sa;Password={Password};TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True";
        using var provider = CreateProvider(options => options.UseSqlServer(connection, sql =>
        {
            sql.MigrationsAssembly(typeof(FilterStatisticsBackfillIndex).Assembly.GetName().Name);
            sql.MigrationsHistoryTable("__RuntimeStorageMigrationsHistory", "Runtime");
        }), new RuntimeSqlServerStorageModelCustomizer());
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);

        var legacyIds = new[] { Id.New(), Id.New() };
        var activeId = Id.New();
        var now = DateTime.UtcNow;
        foreach (var (id, revision) in new[] { (legacyIds[0], 0L), (legacyIds[1], 0L), (activeId, 7L) })
            db.OrchestrationInstances.Add(new OrchestrationInstance
            {
                Id = id, RuntimeOrchestrationArtifactId = Id.New(), TriggerIntakeId = Id.New(),
                OrchestrationDefinitionKey = "test", CorrelationId = id.ToString(), SagaId = id.ToString(),
                ExecutionKey = id.ToString(), Status = OrchestrationInstanceStatus.Running,
                StartedOnUtc = now, LastUpdatedOnUtc = now, StatisticsRevision = revision
            });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var before = await PendingIds(db);
        Assert.Equal(legacyIds.OrderBy(id => id.ToString()), before.OrderBy(id => id.ToString()));
        Assert.Null(await IndexFilter(db));

        await migrator.MigrateAsync();
        Assert.Contains("[StatisticsRevision]", await IndexFilter(db), StringComparison.Ordinal);
        Assert.Equal(before, await PendingIds(db));
        using (var firstScope = provider.CreateScope())
        using (var staleScope = provider.CreateScope())
        {
            var first = firstScope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
            var stale = staleScope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
            var firstRow = await first.OrchestrationInstances.SingleAsync(x => x.Id == activeId);
            var staleRow = await stale.OrchestrationInstances.SingleAsync(x => x.Id == activeId);
            firstRow.StatisticsRevision++;
            staleRow.StatisticsRevision++;
            await first.SaveChangesAsync();
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        }

        await migrator.MigrateAsync(PreviousMigration);
        Assert.Null(await IndexFilter(db));
        Assert.Equal(before, await PendingIds(db));
        await migrator.MigrateAsync();
        var backfill = scope.ServiceProvider.GetRequiredService<ISagaStatisticsBackfill>();
        Assert.False(await backfill.ProcessAsync(1));
        Assert.False(await backfill.ProcessAsync(1));
        Assert.True(await backfill.ProcessAsync(1));
        Assert.Empty(await PendingIds(db));
        Assert.Equal(8, await db.OrchestrationInstances.AsNoTracking().Where(x => x.Id == activeId)
            .Select(x => x.StatisticsRevision).SingleAsync());
    }

    private static ServiceProvider CreateProvider(Action<DbContextOptionsBuilder> configure, RuntimeSqlServerStorageModelCustomizer? customizer = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.AddOrchestratorRuntimeStorageEntityFramework(configure,
            options => options.ConfigureModel = customizer is null ? null : customizer.Configure);
        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateSqlProvider(Action<ModelBuilder> configureModel)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.AddOrchestratorRuntimeStorageEntityFramework(
            options => options.UseSqlServer("Server=localhost;Database=unused;User Id=sa;Password=unused;TrustServerCertificate=True"),
            options => options.ConfigureModel = configureModel);
        return services.BuildServiceProvider();
    }

    private static Microsoft.EntityFrameworkCore.Metadata.IIndex RevisionIndex(RuntimeDbContext db)
        => db.Model.FindEntityType(typeof(OrchestrationInstance))!.GetIndexes()
            .Single(index => index.Properties.Count == 1 && index.Properties[0].Name == nameof(OrchestrationInstance.StatisticsRevision));

    private static Task<Id[]> PendingIds(RuntimeDbContext db)
        => db.OrchestrationInstances.AsNoTracking().Where(x => x.StatisticsRevision == 0)
            .OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync();

    private static async Task<string?> IndexFilter(RuntimeDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT i.filter_definition FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'[Runtime].[OrchestrationInstances]') AND i.name = N'IX_OrchestrationInstances_StatisticsRevision'";
        var result = await command.ExecuteScalarAsync();
        Assert.NotNull(result);
        return result is DBNull ? null : Assert.IsType<string>(result);
    }

    private static async Task WaitForSqlServer(string connectionString)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                return;
            }
            catch (SqlException) when (attempt < 59)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }
}
