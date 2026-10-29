using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class RuntimeInstanceCatalogTests
{
    public static IEnumerable<object[]> SortCases()
    {
        foreach (var relational in new[] { false, true })
            foreach (var sort in Enum.GetValues<RuntimeInstanceCatalogSort>())
                foreach (var descending in new[] { false, true })
                    yield return [relational, sort, descending];
    }

    [Theory]
    [MemberData(nameof(SortCases))]
    public async Task SupportsAllSortOrders(bool relational, RuntimeInstanceCatalogSort sort, bool descending)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        if (relational) await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        if (relational) services.AddOrchestratorRuntimeStorageEntityFramework(options => options.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        if (relational) await scope.ServiceProvider.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var catalog = Assert.IsAssignableFrom<IRuntimeInstanceCatalogRepository>(repository);
        var now = DateTime.UtcNow;
        await repository.Create(Instance("a", now.AddMinutes(-1), OrchestrationInstanceStatus.Created));
        await repository.Create(Instance("b", now, OrchestrationInstanceStatus.Running));

        var page = await catalog.GetPage(new RuntimeInstanceCatalogQuery(1, 1, Sort: sort, Descending: descending));

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(descending ? "b" : "a", Assert.Single(page.Items).OrchestrationDefinitionKey);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FiltersBeforePagingAndOrdersDeterministically(bool relational)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        if (relational) await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        if (relational) services.AddOrchestratorRuntimeStorageEntityFramework(options => options.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        if (relational) await scope.ServiceProvider.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var catalog = Assert.IsAssignableFrom<IRuntimeInstanceCatalogRepository>(repository);
        var now = DateTime.UtcNow;
        foreach (var (key, status, age) in new[]
        {
            ("sale.one", OrchestrationInstanceStatus.Running, 1),
            ("sale.two", OrchestrationInstanceStatus.Running, 2),
            ("sale.three", OrchestrationInstanceStatus.Running, 3),
            ("sale.four", OrchestrationInstanceStatus.Completed, 4),
            ("other", OrchestrationInstanceStatus.Running, 5)
        })
        {
            await repository.Create(new OrchestrationInstance
            {
                Id = Id.New(), RuntimeOrchestrationArtifactId = Id.New(), TriggerIntakeId = Id.New(),
                OrchestrationDefinitionKey = key, CorrelationId = $"corr-{key}", SagaId = $"saga-{key}",
                ExecutionKey = $"exec-{key}", Status = status, StartedOnUtc = now.AddMinutes(-age), LastUpdatedOnUtc = now.AddMinutes(-age)
            });
        }

        var page = await catalog.GetPage(new RuntimeInstanceCatalogQuery(2, 2, "sale", OrchestrationInstanceStatus.Running));

        Assert.Equal(3, page.TotalCount);
        Assert.Equal("sale.three", Assert.Single(page.Items).OrchestrationDefinitionKey);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FiltersByTimeAndActiveStatusBeforePaging(bool relational)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        if (relational) await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        if (relational) services.AddOrchestratorRuntimeStorageEntityFramework(options => options.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        if (relational) await scope.ServiceProvider.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var catalog = Assert.IsAssignableFrom<IRuntimeInstanceCatalogRepository>(repository);
        var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        await repository.Create(Instance("old-idle", start.AddDays(-1), OrchestrationInstanceStatus.Idle));
        var running = Instance("start-running", start, OrchestrationInstanceStatus.Running);
        await repository.Create(running);
        await repository.Create(Instance("middle-completed", start.AddHours(1), OrchestrationInstanceStatus.Completed));
        await repository.Create(Instance("end-created", start.AddDays(1), OrchestrationInstanceStatus.Created));

        var recent = await catalog.GetPage(new RuntimeInstanceCatalogQuery(StartedFromUtc: start, StartedBeforeUtc: start.AddDays(1)));
        Assert.Equal(2, recent.TotalCount);
        Assert.Equal(new[] { "middle-completed", "start-running" }, recent.Items.Select(x => x.OrchestrationDefinitionKey));

        var active = await catalog.GetPage(new RuntimeInstanceCatalogQuery(ActiveOnly: true));
        Assert.Equal(3, active.TotalCount);
        Assert.Contains(active.Items, x => x.OrchestrationDefinitionKey == "old-idle");
        Assert.DoesNotContain(active.Items, x => x.Status == OrchestrationInstanceStatus.Completed);

        var boundedActive = await catalog.GetPage(new RuntimeInstanceCatalogQuery(PageSize: 1, StartedFromUtc: start,
            StartedBeforeUtc: start.AddDays(1), ActiveOnly: true));
        Assert.Equal(1, boundedActive.TotalCount);
        Assert.Equal("start-running", Assert.Single(boundedActive.Items).OrchestrationDefinitionKey);

        var countsRepository = Assert.IsAssignableFrom<IRuntimeInstanceStatusCountsRepository>(repository);
        var counts = await countsRepository.GetStatusCounts(new RuntimeInstanceCatalogQuery(PageSize: 1,
            StartedFromUtc: start, StartedBeforeUtc: start.AddDays(1)));
        Assert.Equal(1, counts[OrchestrationInstanceStatus.Running]);
        Assert.Equal(1, counts[OrchestrationInstanceStatus.Completed]);
        Assert.Equal(2, counts.Values.Sum());
        Assert.False(counts.ContainsKey(OrchestrationInstanceStatus.Idle));

        var byId = await catalog.GetPage(new RuntimeInstanceCatalogQuery(SearchText: running.Id.ToString()));
        Assert.Equal(running.Id, Assert.Single(byId.Items).Id);
    }

    private static OrchestrationInstance Instance(string key, DateTime timestamp, OrchestrationInstanceStatus status)
        => new()
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = Id.New(), TriggerIntakeId = Id.New(),
            OrchestrationDefinitionKey = key, CorrelationId = key, SagaId = key, ExecutionKey = key,
            Status = status, StartedOnUtc = timestamp, LastUpdatedOnUtc = timestamp
        };
}
