namespace Krackend.Sagas.Orchestrations.Tests.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Repositories;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Sieve.Services;

public sealed class DistributionEnvironmentCatalogTests
{
    [Fact]
    public async Task ApplicationServiceForwardsSearchAndMapsPage()
    {
        var repository = Substitute.For<IDistributionEnvironmentRepository>();
        var environment = new DistributionEnvironment { Id = Id.New(), Name = "Production", Code = "prod", Description = "Main", IsEnabled = true };
        repository.Search(Arg.Any<PagedSettings>(), "prod", "key", true, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<DistributionEnvironment>(2, 3, 5, 2, [environment]));

        var result = await new DistributionEnvironmentApplicationService(repository).Search(
            new ApplicationPagedSettings { PageNumber = 2, PageSize = 2 }, "prod", "key", true);

        Assert.Equal(5, result.TotalRows);
        Assert.Equal("Production", Assert.Single(result.Rows).Name);
        await repository.Received(1).Search(Arg.Is<PagedSettings>(s => s.PageNumber == 2 && s.PageSize == 2),
            "prod", "key", true, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("key", false)]
    [InlineData("key", true)]
    [InlineData("code", false)]
    [InlineData("code", true)]
    [InlineData("status", false)]
    [InlineData("status", true)]
    [InlineData("updated", false)]
    [InlineData("updated", true)]
    [InlineData("displayName", false)]
    [InlineData("displayName", true)]
    public async Task SearchFiltersAndPagesInRelationalStorage(string sort, bool descending)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>().UseSqlite(connection).Options;
        await using var context = new ControlPlaneDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var repository = new DistributionEnvironmentRepository(context, Substitute.For<ISieveProcessor>());
        for (var i = 0; i < 3; i++)
            await repository.Create(new DistributionEnvironment
            {
                Id = Id.New(), Name = $"Sample {i}", Code = $"env_{i}", Description = "testing",
                IsEnabled = i != 1, CreatedAtUtc = DateTime.UtcNow.AddMinutes(-i)
            });

        var result = await repository.Search(new PagedSettings(2, 2, [], []), "Sample", sort, descending);

        Assert.Equal(3, result.TotalRows);
        Assert.Single(result.Rows);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(3, (await repository.Search(null!, "env_", sort, descending)).TotalRows);
        Assert.Equal(3, (await repository.Search(null!, "testing", sort, descending)).TotalRows);
        Assert.Equal(2, (await repository.SearchEnabled(null!, "testing")).TotalRows);
        Assert.Empty((await repository.Search(null!, "missing", sort, descending)).Rows);
    }
}
