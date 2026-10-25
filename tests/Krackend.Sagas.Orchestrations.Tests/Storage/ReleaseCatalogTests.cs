namespace Krackend.Sagas.Orchestrations.Tests.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Repositories;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Sieve.Services;

public sealed class ReleaseCatalogTests
{
    [Theory]
    [InlineData("requestedBy", false)]
    [InlineData("requestedBy", true)]
    [InlineData("strategy", false)]
    [InlineData("strategy", true)]
    [InlineData("status", false)]
    [InlineData("status", true)]
    [InlineData("created", false)]
    [InlineData("created", true)]
    public async Task SearchFiltersAndPagesInDatabaseAndDetailsLoadTargets(string sort, bool descending)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>().UseSqlite(connection).Options;
        await using var context = new ControlPlaneDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var repository = new ReleaseRepository(context, Substitute.For<ISieveProcessor>());
        var environmentId = Id.New();
        context.DistributionEnvironments.Add(new DistributionEnvironmentEntity
        {
            Id = environmentId, Name = "Testing", Code = "testing", Description = "Test", IsEnabled = true
        });
        await context.SaveChangesAsync();
        var nodeId = Id.New();
        await new RuntimeNodeRepository(context, Substitute.For<ISieveProcessor>()).Create(new RuntimeNode
        {
            Id = nodeId, Name = "Node", Code = "test.node", EnvironmentId = environmentId,
            Description = "Test", RegisteredAtUtc = DateTime.UtcNow
        });
        var artifactId = Id.New();
        await new ArtifactRepository(context, Substitute.For<ISieveProcessor>()).Create(new Artifact
        {
            Id = artifactId, OrchestrationDefinitionId = "sales", OrchestrationVersionId = "version",
            OrchestrationDisplayName = "Sales", VersionLabel = "Test", VersionNumber = "1.0.0",
            ArtifactType = "orchestration-version.new", SchemaVersion = "1", Payload = "{}", Metadata = "{}",
            SourceEvent = "test", SourceVersion = "1", Checksum = "checksum", CreatedAtUtc = DateTime.UtcNow
        });
        var targetRepository = new ReleaseTargetRepository(context, Substitute.For<ISieveProcessor>());
        var releaseWithTarget = default(Id);
        var targetId = Id.New();
        for (var i = 0; i < 3; i++)
        {
            var release = new Release
            {
                Id = Id.New(), ArtifactId = artifactId, OrchestrationDefinitionId = "sales",
                RequestedBy = $"operator-{i}", Strategy = "Direct", Status = ReleaseStatus.InProgress,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-i)
            };
            await repository.Create(release, [new ReleasePlanTarget
            {
                Id = Id.New(), ReleaseId = release.Id, RuntimeNodeId = nodeId, Status = ReleaseStatus.InProgress,
                CreatedAtUtc = DateTime.UtcNow, Notes = string.Empty
            }]);
            if (i == 0)
            {
                releaseWithTarget = release.Id;
                await targetRepository.Create(new ReleaseTarget
                {
                    Id = targetId, ReleaseId = release.Id, ArtifactId = artifactId, RuntimeNodeId = nodeId,
                    RolloutGroup = "Direct", Status = ReleaseTargetStatus.PushScheduled,
                    ActivationStatus = ActivationStatus.NotActivated, AssignedAtUtc = DateTime.UtcNow
                });
            }
        }

        var result = await repository.Search(new PagedSettings(2, 2, [], []), "sales", "operator", sort, descending);
        Assert.Equal(3, result.TotalRows);
        var row = Assert.Single(result.Rows);
        var service = new ReleaseApplicationService(repository, Substitute.For<IReleaseTargetRepository>(),
            Substitute.For<IArtifactRepository>(), Substitute.For<IRuntimeNodeRepository>(), Substitute.For<IOrchestrationNodePolicyRepository>());
        var summaries = await service.Search(new ApplicationPagedSettings { PageNumber = 1, PageSize = 2 }, "sales", "operator", sort, descending);
        Assert.Equal(3, summaries.TotalRows);
        Assert.All(summaries.Rows, item => Assert.Single(item.Targets));
        Assert.Equal(3, Assert.Single(await service.GetSummaries(["sales", "other"])).Count);
        Assert.Empty(await service.GetSummaries([]));
        Assert.Single((await service.GetById(row.Id.ToString())).Targets);
        var targetService = new ReleaseTargetApplicationService(targetRepository);
        for (var i = 0; i < 3; i++)
        {
            await targetRepository.AddAttempt(new ReleaseAttempt
            {
                Id = Id.New(), ReleaseTargetId = targetId, Action = "Push", InitiatedBy = "operator",
                StartedAtUtc = new DateTime(2026, 1, 1).AddMinutes(i), Succeeded = i == 2,
                ErrorMessage = i == 2 ? null : "failed delivery"
            });
        }
        var firstAttempt = await targetService.SearchAttempts(targetId.ToString(), 1, 1, "failed", false);
        var secondAttempt = await targetService.SearchAttempts(targetId.ToString(), 2, 1, "failed", false);
        Assert.Equal(2, firstAttempt.TotalRows);
        Assert.Equal(2, firstAttempt.TotalPages);
        Assert.True(Assert.Single(firstAttempt.Rows).StartedAtUtc < Assert.Single(secondAttempt.Rows).StartedAtUtc);
        var latestAttempt = await targetService.SearchAttempts(targetId.ToString(), 1, 1, "", true);
        Assert.True(Assert.Single(latestAttempt.Rows).Succeeded);
        Assert.Empty((await targetService.SearchAttempts(targetId.ToString(), 1, 25, "missing", false)).Rows);
        var assignments = await targetService.Search(1, 1, "Node", "status", false);
        Assert.Equal(1, assignments.TotalRows);
        Assert.Equal(nodeId.ToString(), Assert.Single(assignments.Rows).RuntimeNodeId);
        Assert.Equal("Node", Assert.Single(assignments.Rows).RuntimeNodeName);
        Assert.Single((await targetService.Search(1, 1, releaseWithTarget.ToString(), "assigned", true)).Rows);
        Assert.Empty((await targetService.Search(1, 1, "not-found", "assigned", false)).Rows);
        Assert.Equal(releaseWithTarget.ToString(), Assert.Single(await targetService.GetByRelease(releaseWithTarget.ToString())).ReleaseId);
        Assert.Empty(await targetService.GetByRelease(Id.New().ToString()));
        Assert.Equal(releaseWithTarget.ToString(), Assert.Single(await targetService.GetByReleases([releaseWithTarget.ToString(), Id.New().ToString()])).ReleaseId);
        Assert.Empty(await targetService.GetByReleases([]));
        Assert.Empty((await repository.Search(null!, "other", null!, sort, descending)).Rows);
    }
}
