using DotNet.Testcontainers.Builders;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class MongoCatalogSearchTests
{
    // Keep Testcontainers deterministic: newer MongoDB 8.0 images deliberately
    // refuse the hosted Linux kernels used by GitHub Actions (SERVER-121912).
    private const string MongoImage = "mongo:8.0.4";

    [Fact]
    public async Task RuntimeArtifactGroupsAreFilteredAndPagedInMongo()
    {
        await using var mongo = new ContainerBuilder()
            .WithImage(MongoImage)
            .WithPortBinding(27017, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(27017))
            .Build();
        await mongo.StartAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.AddOrchestratorRuntimeStorageEntityFramework(options =>
            options.UseMongoDB($"mongodb://{mongo.Hostname}:{mongo.GetMappedPublicPort(27017)}", "runtime_catalog_tests"));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        await context.Database.EnsureCreatedAsync();
        var artifacts = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>();
        var catalog = Assert.IsAssignableFrom<IRuntimeArtifactUiCatalog>(artifacts);
        foreach (var (key, version, active) in new[] { ("sales.created", 1, true), ("sales.created", 2, false), ("billing.created", 1, true) })
            await artifacts.Upsert(new RuntimeOrchestrationArtifact
            {
                Id = Id.New(), OrchestrationDefinitionKey = key, SourceOrchestrationVersionId = Id.New(),
                ArtifactType = "orchestration-version-snapshot", Version = new SemanticVersion(version, 0, 0),
                ArtifactChecksum = new Checksum($"checksum-{key}-{version}"), ArtifactPayload = JsonNode.Parse("{}")!,
                Status = RuntimeOrchestrationArtifactStatus.Ready, IsActive = active, DeployedOnUtc = DateTime.UtcNow,
                LastReleaseId = $"release-{version}", LastControlPlaneArtifactId = $"artifact-{version}"
            });
        var page = await catalog.GetGroups(1, 1, "sales");
        Assert.Equal(1, page.TotalCount);
        var group = Assert.Single(page.Items);
        Assert.Equal("sales.created", group.Key);
        Assert.Equal(2, group.VersionCount);
        Assert.Equal(1, group.ActiveCount);
        var groups = await catalog.GetGroups(1, 1, "");
        Assert.Equal(2, groups.TotalCount);
        Assert.Equal("billing.created", Assert.Single(groups.Items).Key);
        Assert.Equal("sales.created", Assert.Single((await catalog.GetGroups(2, 1, "")).Items).Key);
        Assert.Empty((await catalog.GetGroups(1, 1, "missing")).Items);
        var versions = await catalog.GetVersions("sales.created", 1, 1, "");
        Assert.Equal(2, versions.TotalCount);
        Assert.Single(versions.Items);
        Assert.Single((await catalog.GetVersions("sales.created", 1, 25, "1.0.0")).Items);
        Assert.Single((await catalog.GetVersions("sales.created", 1, 25, "release-2")).Items);
    }

    [Fact]
    public async Task DomainsAndTeamsFilterCaseInsensitivelyBeforePaging()
    {
        await using var mongo = new ContainerBuilder()
            .WithImage(MongoImage)
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
        var releases = scope.ServiceProvider.GetRequiredService<IReleaseRepository>();
        var latest = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var environmentId = Id.New();
        context.DistributionEnvironments.Add(new DistributionEnvironmentEntity { Id = environmentId, Name = "Test", Code = "test", Description = "", IsEnabled = true });
        await context.SaveChangesAsync();
        var nodeId = Id.New();
        await scope.ServiceProvider.GetRequiredService<IRuntimeNodeRepository>().Create(new RuntimeNode
        {
            Id = nodeId, Name = "Node", Code = "test-node", EnvironmentId = environmentId,
            Description = "Test", RegisteredAtUtc = latest, Status = RuntimeNodeStatus.Enabled
        });
        context.OrchestrationAllowedRuntimeNodes.Add(new OrchestrationAllowedRuntimeNodeEntity
        {
            Id = Id.New(), RuntimeNodeId = nodeId, OrchestrationDefinitionId = "sales", CreatedAtUtc = latest, CreatedBy = "test"
        });
        await context.SaveChangesAsync();
        var nodes = scope.ServiceProvider.GetRequiredService<IRuntimeNodeRepository>();
        Assert.Equal(nodeId, Assert.Single((await nodes.SearchEnabled(new PagedSettings(1, 1, [], []), "Node", "", false)).Rows).Id);
        Assert.Equal(nodeId, Assert.Single((await nodes.SearchAllowed(new PagedSettings(1, 1, [], []), "sales", "Node")).Rows).Id);
        Assert.Equal(nodeId, Assert.Single((await nodes.SearchAllowed(new PagedSettings(1, 1, [], []), "sales", "")).Rows).Id);
        Assert.Empty((await nodes.SearchAllowed(new PagedSettings(1, 1, [], []), "other", "Node")).Rows);
        Assert.Empty((await nodes.SearchAllowed(new PagedSettings(1, 1, [], []), "other", "")).Rows);
        var targetRepository = scope.ServiceProvider.GetRequiredService<IReleaseTargetRepository>();
        var releaseId = Id.New();
        var artifactId = Id.New();
        await releases.Create(new Release { Id = releaseId, ArtifactId = artifactId, OrchestrationDefinitionId = "sales",
            RequestedBy = "test", Strategy = "Direct", Status = ReleaseStatus.InProgress, CreatedAtUtc = latest },
            [new ReleasePlanTarget { Id = Id.New(), ReleaseId = releaseId, RuntimeNodeId = nodeId,
                Status = ReleaseStatus.InProgress, CreatedAtUtc = latest, Notes = "" }]);
        await targetRepository.Create(new ReleaseTarget { Id = Id.New(), ReleaseId = releaseId, ArtifactId = artifactId,
            RuntimeNodeId = nodeId, Status = ReleaseTargetStatus.Activated, ActivationStatus = ActivationStatus.Activated,
            RolloutGroup = "Direct", AssignedAtUtc = latest });
        var failedReleaseId = Id.New();
        await releases.Create(new Release { Id = failedReleaseId, ArtifactId = artifactId, OrchestrationDefinitionId = "sales",
            RequestedBy = "test", Strategy = "Direct", Status = ReleaseStatus.InProgress, CreatedAtUtc = latest.AddDays(-1) },
            [new ReleasePlanTarget { Id = Id.New(), ReleaseId = failedReleaseId, RuntimeNodeId = nodeId,
                Status = ReleaseStatus.Failed, CreatedAtUtc = latest, Notes = "" }]);
        var summary = Assert.Single(await releases.GetSummaries(["sales", "missing", "sales"]));
        Assert.Equal(2, summary.Count);
        Assert.Equal(latest, summary.LatestCreatedAtUtc);
        Assert.Empty(await releases.GetSummaries([]));
        var targets = await releases.SearchTargetDetails(releaseId, new PagedSettings(1, 25, [], []));
        Assert.Equal("Node", Assert.Single(targets.Rows).Name);
        Assert.Equal("Test", Assert.Single(targets.Rows).Environment);
        Assert.Empty((await releases.SearchTargetDetails(releaseId, new PagedSettings(2, 25, [], []))).Rows);
        var failedTarget = Assert.Single((await releases.SearchTargetDetails(failedReleaseId, new PagedSettings(1, 25, [], []))).Rows);
        Assert.Equal("Failed", failedTarget.Status);
        Assert.Equal("", failedTarget.TargetId);
        Assert.True(Assert.Single(await releases.GetEffectiveStatuses([releaseId])).AllActivated);
        var failed = Assert.Single(await releases.GetEffectiveStatuses([failedReleaseId]));
        Assert.False(failed.AllActivated);
        Assert.True(failed.AnyFailed);
        Assert.Empty(await releases.GetEffectiveStatuses([Id.New()]));
        await targetRepository.Create(new ReleaseTarget { Id = Id.New(), ReleaseId = releaseId, ArtifactId = artifactId,
            RuntimeNodeId = nodeId, Status = ReleaseTargetStatus.Failed, ActivationStatus = ActivationStatus.ActivationFailed,
            RolloutGroup = "Direct", AssignedAtUtc = latest.AddMinutes(1) });
        var latestStatus = Assert.Single(await releases.GetEffectiveStatuses([releaseId]));
        Assert.False(latestStatus.AllActivated);
        Assert.True(latestStatus.AnyFailed);
        Assert.Equal("Failed", Assert.Single((await releases.SearchTargetDetails(releaseId, new PagedSettings(1, 25, [], []))).Rows).Status);
        Assert.Empty((await domains.GetAll(settings, "%")).Rows);
        Assert.Empty((await teams.GetAll(1, 10, "%")).Rows);
    }
}
