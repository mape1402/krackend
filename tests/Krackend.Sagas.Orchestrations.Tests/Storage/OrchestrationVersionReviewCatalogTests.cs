namespace Krackend.Sagas.Orchestrations.Tests.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Repositories;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

public sealed class OrchestrationVersionReviewCatalogTests
{
    [Theory]
    [InlineData("operation", false)]
    [InlineData("operation", true)]
    [InlineData("orchestration", false)]
    [InlineData("orchestration", true)]
    [InlineData("version", false)]
    [InlineData("version", true)]
    [InlineData("approval", false)]
    [InlineData("approval", true)]
    [InlineData("application", false)]
    [InlineData("application", true)]
    [InlineData("requested", false)]
    [InlineData("requested", true)]
    public async Task SearchUsesRelationalFilteringAndPaging(string sort, bool descending)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>().UseSqlite(connection).Options;
        await using var context = new ControlPlaneDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var definitionId = Id.New();
        var versionId = Id.New();
        context.Set<OrchestrationDefinitionEntity>().Add(new OrchestrationDefinitionEntity
        {
            Id = definitionId, Key = "sales_order", Name = "Sales order", Domain = "sales", OwnerTeam = "team", CreatedBy = "test"
        });
        context.Set<OrchestrationVersionEntity>().Add(new OrchestrationVersionEntity
        {
            Id = versionId, OrchestrationDefinitionId = definitionId, Version = "1.2.3", DraftSlot = "closed:test",
            Checksum = "checksum", CreatedBy = "test"
        });
        var repository = new OrchestrationVersionReviewRepository(context);
        for (var i = 0; i < 3; i++)
            await repository.Create(new OrchestrationVersionReview
            {
                Id = Id.New(), OrchestrationVersionId = versionId, Operation = OrchestrationVersionReviewOperation.Deploy,
                ExpectedChecksum = "checksum", RequestedBy = "tester", RequestedOnUtc = DateTime.UtcNow.AddMinutes(-i),
                ApplicationStatus = OrchestrationVersionReviewApplicationStatus.Applied
            });

        var result = await repository.Search(new PagedSettings(2, 2, [], []), "Sales", sort, descending);

        Assert.Equal(3, result.TotalRows);
        Assert.Single(result.Rows);
        Assert.Equal(2, result.PageNumber);
        Assert.Empty((await repository.Search(null, "not-present", sort, descending)).Rows);
        Assert.Equal(3, (await repository.Search(null, "Pending", sort, descending)).TotalRows);
    }
}
