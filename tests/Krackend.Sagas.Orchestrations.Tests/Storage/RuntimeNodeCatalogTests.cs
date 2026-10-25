namespace Krackend.Sagas.Orchestrations.Tests.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Repositories;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Sieve.Services;

public sealed class RuntimeNodeCatalogTests
{
    [Fact]
    public async Task SearchEnabledExcludesDisabledAndDeletedNodesBeforePaging()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>().UseSqlite(connection).Options;
        await using var context = new ControlPlaneDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var environmentId = Id.New();
        context.DistributionEnvironments.Add(new DistributionEnvironmentEntity
        {
            Id = environmentId, Name = "Testing", Code = "testing", Description = "Test", IsEnabled = true
        });
        await context.SaveChangesAsync();
        var repository = new RuntimeNodeRepository(context, Substitute.For<ISieveProcessor>());
        var ids = new[] { Id.New(), Id.New(), Id.New() };
        for (var i = 0; i < ids.Length; i++)
            await repository.Create(new RuntimeNode
            {
                Id = ids[i], Name = $"Node {i}", Code = $"node.{i}", EnvironmentId = environmentId,
                Description = "Sample", RegisteredAtUtc = DateTime.UtcNow
            });
        await repository.SetStatus(ids[0], RuntimeNodeStatus.Enabled);
        await repository.SetStatus(ids[1], RuntimeNodeStatus.Enabled);
        await repository.SoftDelete(ids[1]);
        context.OrchestrationAllowedRuntimeNodes.AddRange(
            new OrchestrationAllowedRuntimeNodeEntity { Id = Id.New(), OrchestrationDefinitionId = "sales", RuntimeNodeId = ids[0], CreatedBy = "test" },
            new OrchestrationAllowedRuntimeNodeEntity { Id = Id.New(), OrchestrationDefinitionId = "sales", RuntimeNodeId = ids[1], CreatedBy = "test" },
            new OrchestrationAllowedRuntimeNodeEntity { Id = Id.New(), OrchestrationDefinitionId = "sales", RuntimeNodeId = ids[2], CreatedBy = "test" });
        await context.SaveChangesAsync();

        var result = await repository.SearchEnabled(new PagedSettings(1, 1, [], []), "Node", "displayName", false);
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(ids[0], Assert.Single(result.Rows).Id);
        var service = new RuntimeNodeApplicationService(repository, new DistributionEnvironmentRepository(context, Substitute.For<ISieveProcessor>()));
        Assert.Single((await service.SearchEnabled(new ApplicationPagedSettings { PageNumber = 1, PageSize = 25 }, "Node", "displayName", false)).Rows);
        Assert.Equal(ids[0], Assert.Single((await repository.SearchAllowed(new PagedSettings(1, 1, [], []), "sales", "Node")).Rows).Id);
        Assert.Empty((await repository.SearchAllowed(null!, "other", "Node")).Rows);
        Assert.Empty((await repository.SearchAllowed(null!, "sales", "missing")).Rows);
        Assert.Single((await service.SearchAllowed(new ApplicationPagedSettings { PageNumber = 1, PageSize = 25 }, "sales", "Node")).Rows);
    }

    [Theory]
    [InlineData("key", false)]
    [InlineData("key", true)]
    [InlineData("environment", false)]
    [InlineData("environment", true)]
    [InlineData("status", false)]
    [InlineData("status", true)]
    [InlineData("displayName", false)]
    [InlineData("displayName", true)]
    public async Task SearchFiltersAndPagesNodesInRelationalStorage(string sort, bool descending)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>().UseSqlite(connection).Options;
        await using var context = new ControlPlaneDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var environmentId = Id.New();
        context.DistributionEnvironments.Add(new DistributionEnvironmentEntity
        {
            Id = environmentId, Name = "Testing", Code = "testing", Description = "Test", IsEnabled = true
        });
        await context.SaveChangesAsync();
        var repository = new RuntimeNodeRepository(context, Substitute.For<ISieveProcessor>());
        for (var i = 0; i < 3; i++)
            await repository.Create(new RuntimeNode
            {
                Id = Id.New(), Name = $"Node {i}", Code = $"node.{i}", EnvironmentId = environmentId,
                Description = "Sample", RegisteredAtUtc = DateTime.UtcNow
            });

        var result = await repository.Search(new PagedSettings(2, 2, [], []), "Testing", sort, descending);
        Assert.Equal(3, result.TotalRows);
        Assert.Single(result.Rows);
        Assert.Equal(3, (await repository.Search(null!, "Sample", sort, descending)).TotalRows);
        Assert.Empty((await repository.Search(null!, "absent", sort, descending)).Rows);
        var service = new RuntimeNodeApplicationService(repository, new DistributionEnvironmentRepository(context, Substitute.For<ISieveProcessor>()));
        var node = Assert.Single(result.Rows);
        Assert.Equal(node.Name, (await service.GetById(node.Id.ToString())).Name);
        Assert.Equal(3, (await service.Search(new ApplicationPagedSettings { PageNumber = 1, PageSize = 25 }, "Testing", sort, descending)).TotalRows);
        await repository.SoftDelete(node.Id);
        Assert.Equal(2, (await repository.Search(null!, "Testing", sort, descending)).TotalRows);
    }
}
