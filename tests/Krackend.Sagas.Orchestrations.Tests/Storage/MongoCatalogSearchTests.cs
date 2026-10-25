using DotNet.Testcontainers.Builders;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class MongoCatalogSearchTests
{
    [Fact]
    public async Task DomainsAndTeamsFilterCaseInsensitivelyBeforePaging()
    {
        await using var mongo = new ContainerBuilder()
            .WithImage("mongo:8.0")
            .WithPortBinding(27017, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(27017))
            .Build();
        await mongo.StartAsync();

        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<Sieve.Models.SieveOptions>(_ => { });
        services.AddOrchestratorControlPlaneStorageEntityFramework(options =>
            options.UseMongoDB($"mongodb://{mongo.Hostname}:{mongo.GetMappedPublicPort(27017)}", "catalog_tests"));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        await context.Database.EnsureCreatedAsync();
        var domains = scope.ServiceProvider.GetRequiredService<IDomainRepository>();
        var teams = scope.ServiceProvider.GetRequiredService<ITeamRepository>();
        foreach (var (key, active) in new[] { ("match_1", false), ("match_2", true), ("match_3", true), ("other", true) })
        {
            await domains.Upsert(new Domain { Id = Id.New(), Key = key, DisplayName = key, Description = "", IsActive = active, CreatedOnUtc = DateTime.UtcNow });
            await teams.Upsert(new Team { Id = Id.New(), Key = key, DisplayName = key, Description = "", IsActive = active, CreatedOnUtc = DateTime.UtcNow });
        }

        var settings = new PagedSettings(1, 1, [], [new QuerySort("displayName", false), new QuerySort("key", false)]);
        var domainPage = await domains.SearchActive(settings, "MATCH");
        var teamPage = await teams.SearchActive(1, 1, "MATCH");
        Assert.Equal(2, domainPage.TotalRows);
        Assert.Equal("match_2", Assert.Single(domainPage.Rows).Key);
        Assert.Equal(2, teamPage.TotalRows);
        Assert.Equal("match_2", Assert.Single(teamPage.Rows).Key);
        Assert.Equal("match_3", Assert.Single((await teams.SearchActive(2, 1, "MATCH")).Rows).Key);
        Assert.Equal(3, (await teams.GetAll(1, 10, "MATCH")).TotalRows);
        var members = scope.ServiceProvider.GetRequiredService<ITeamMemberRepository>();
        var teamId = Assert.Single(teamPage.Rows).Id;
        var emptyTeamId = Assert.Single((await teams.SearchActive(2, 1, "MATCH")).Rows).Id;
        await members.Add(new TeamMember { Id = Id.New(), TeamId = teamId, ExternalUserId = "user", DisplayName = "User" });
        var counts = await members.CountByTeams([teamId, emptyTeamId, teamId]);
        Assert.Equal(1, counts[teamId]);
        Assert.False(counts.ContainsKey(emptyTeamId));
        Assert.Empty((await domains.GetAll(settings, "%")).Rows);
        Assert.Empty((await teams.GetAll(1, 10, "%")).Rows);
    }
}
