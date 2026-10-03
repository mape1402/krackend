namespace Krackend.Sagas.Orchestrations.Tests.Distribution;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class DistributionEnvironmentTests
{
    [Fact]
    public async Task ApplicationServiceCreatesUpdatesTrimsAndMapsEnvironments()
    {
        var repository = new RecordingDistributionEnvironmentRepository();
        var service = new DistributionEnvironmentApplicationService(repository);

        var createdId = await service.Upsert(new UpsertDistributionEnvironmentInput(
            "",
            " Development ",
            " dev ",
            " local dev ",
            true));
        var created = Assert.Single(repository.Created);
        created.CreatedAtUtc = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        repository.Environments[created.Id] = created;

        var updatedId = await service.Upsert(new UpsertDistributionEnvironmentInput(
            createdId,
            " Production ",
            " prod ",
            null!,
            false));
        var page = await service.GetAll(new ApplicationPagedSettings { PageNumber = 2, PageSize = 5 });

        Assert.Equal(createdId, updatedId);
        Assert.Equal("Development", created.Name);
        Assert.Equal("dev", created.Code);
        Assert.Equal("local dev", created.Description);
        var updated = Assert.Single(repository.Updated);
        Assert.Equal("Production", updated.Name);
        Assert.Equal("prod", updated.Code);
        Assert.Equal(string.Empty, updated.Description);
        Assert.False(updated.IsEnabled);
        Assert.Equal(new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc), updated.CreatedAtUtc);
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(5, page.PageSize);
        Assert.Equal(1, page.TotalRows);
        Assert.Equal("Production", Assert.Single(page.Rows).Name);
    }

    [Fact]
    public async Task ApplicationServiceRejectsNullInputAndTreatsInvalidIdsAsCreates()
    {
        var repository = new RecordingDistributionEnvironmentRepository();
        var service = new DistributionEnvironmentApplicationService(repository);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.Upsert(null!));
        var id = await service.Upsert(new UpsertDistributionEnvironmentInput(
            "not-a-ulid",
            " QA ",
            " qa ",
            " Quality ",
            true));

        Assert.True(Ulid.TryParse(id, out _));
        Assert.Single(repository.Created);
        Assert.Empty(repository.Updated);
    }

    [Fact]
    public async Task RepositoryPersistsUpdatesPagesAndRejectsDuplicateCodes()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDistributionEnvironmentRepository>();

        var zulu = Environment("Zulu", "zulu");
        await repository.Create(zulu);

        var byId = await repository.GetById(zulu.Id);
        var byCode = await repository.GetByCode("zulu");
        Assert.Equal(zulu.Id, byId.Id);
        Assert.Equal(zulu.Id, byCode.Id);
        Assert.Null(await repository.GetByCode("missing"));

        zulu.Name = "Alpha";
        zulu.Code = "alpha";
        zulu.Description = "Updated";
        zulu.IsEnabled = false;
        await repository.Update(zulu);

        var updated = await repository.GetById(zulu.Id);
        Assert.Equal("Alpha", updated.Name);
        Assert.Equal("Updated", updated.Description);
        Assert.False(updated.IsEnabled);
        Assert.NotNull(updated.UpdatedAtUtc);

        var beta = Environment("Beta", "beta");
        await repository.Create(beta);

        var page = await repository.GetAll(new PagedSettings(1, 1, [], []));
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(2, page.TotalRows);
        Assert.Equal("Alpha", Assert.Single(page.Rows).Name);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.Create(Environment("Alpha Duplicate", "alpha")));
        beta.Code = "alpha";
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.Update(beta));
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<Sieve.Models.SieveOptions>(_ => { });
        services.AddOrchestratorControlPlaneStorageEntityFramework(options =>
            options.UseInMemoryDatabase($"control-plane-distribution-environments-{Guid.NewGuid():N}"));
        return services.BuildServiceProvider();
    }

    private static DistributionEnvironment Environment(string name, string code)
        => new()
        {
            Id = Id.New(),
            Name = name,
            Code = code,
            Description = $"{name} environment",
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow
        };

    private sealed class RecordingDistributionEnvironmentRepository : IDistributionEnvironmentRepository
    {
        public Dictionary<Id, DistributionEnvironment> Environments { get; } = new();

        public List<DistributionEnvironment> Created { get; } = [];

        public List<DistributionEnvironment> Updated { get; } = [];

        public Task Create(DistributionEnvironment environment, CancellationToken cancellationToken = default)
        {
            Created.Add(environment);
            Environments[environment.Id] = environment;
            return Task.CompletedTask;
        }

        public Task Update(DistributionEnvironment environment, CancellationToken cancellationToken = default)
        {
            Updated.Add(environment);
            Environments[environment.Id] = environment;
            return Task.CompletedTask;
        }

        public Task<DistributionEnvironment> GetById(Id environmentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Environments[environmentId]);

        public Task<DistributionEnvironment> GetByCode(string code, CancellationToken cancellationToken = default)
            => Task.FromResult(Environments.Values.FirstOrDefault(x => x.Code == code)!);

        public Task<PagedResult<DistributionEnvironment>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default)
        {
            var rows = Environments.Values.OrderBy(x => x.Name).ToArray();
            return Task.FromResult(new PagedResult<DistributionEnvironment>(
                pagedSettings.PageNumber,
                rows.Length == 0 ? 1 : (int)Math.Ceiling(rows.Length / (double)pagedSettings.PageSize),
                rows.Length,
                pagedSettings.PageSize,
                rows));
        }
    }
}
