namespace Krackend.Sagas.Orchestrations.Tests.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Repositories;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Sieve.Services;

public sealed class ArtifactCatalogTests
{
    [Theory]
    [InlineData("displayName", false)]
    [InlineData("displayName", true)]
    [InlineData("version", false)]
    [InlineData("version", true)]
    [InlineData("artifactType", false)]
    [InlineData("artifactType", true)]
    [InlineData("created", false)]
    [InlineData("created", true)]
    public async Task SearchPagesSummariesAndLoadsPayloadOnlyForDetails(string sort, bool descending)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>().UseSqlite(connection).Options;
        await using var context = new ControlPlaneDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var repository = new ArtifactRepository(context, Substitute.For<ISieveProcessor>());
        for (var i = 0; i < 3; i++)
            await repository.Create(new Artifact
            {
                Id = Id.New(), OrchestrationDefinitionId = "sales", OrchestrationVersionId = $"version.{i}",
                OrchestrationDisplayName = "Sales", VersionLabel = "Sample", VersionNumber = $"1.0.{i}",
                ArtifactType = "orchestration-version.new", SchemaVersion = "1", Payload = "{\"test\":true}",
                Metadata = "{}", SourceEvent = "test", SourceVersion = "1", Checksum = $"checksum.{i}", CreatedAtUtc = DateTime.UtcNow.AddMinutes(-i)
            });

        var result = await repository.Search(new PagedSettings(2, 2, [], []), "sales", "Sample", sort, descending);
        Assert.Equal(3, result.TotalRows);
        var summary = Assert.Single(result.Rows);
        Assert.Null(summary.Payload);
        var service = new ArtifactApplicationService(repository);
        Assert.Equal("{\"test\":true}", (await service.GetById(summary.Id.ToString())).Payload);
        Assert.Equal(3, (await service.Search(new ApplicationPagedSettings { PageNumber = 1, PageSize = 25 }, "sales", "Sales", sort, descending)).TotalRows);
        Assert.Empty((await repository.Search(null!, "other", null!, sort, descending)).Rows);
    }
}
