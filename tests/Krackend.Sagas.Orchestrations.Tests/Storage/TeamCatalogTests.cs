namespace Krackend.Sagas.Orchestrations.Tests.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Security.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

public sealed class TeamCatalogTests
{
    [Theory]
    [InlineData("key", false)]
    [InlineData("key", true)]
    [InlineData("status", false)]
    [InlineData("status", true)]
    [InlineData("displayName", false)]
    [InlineData("displayName", true)]
    public async Task CatalogQueriesAndCountsInRelationalStorage(string sort, bool descending)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>().UseSqlite(connection).Options;
        await using var context = new ControlPlaneDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var teams = new TeamRepository(context);
        var members = new TeamMemberRepository(context);
        for (var i = 0; i < 3; i++)
        {
            var id = Id.New();
            await teams.Upsert(new Team { Id = id, Key = $"team.{i}", DisplayName = $"Sales {i}", Description = "Sample", IsActive = i != 1 });
            await members.Add(new TeamMember { Id = Id.New(), TeamId = id, ExternalUserId = $"user.{i}" });
        }

        var handler = new GetTeamsQueryHandler(teams, members);
        var result = await handler.Handle(new GetTeamsQuery(new ApplicationPagedSettings { PageNumber = 2, PageSize = 2 }, "Sales", true, sort, descending), default);

        Assert.Equal(3, result.TotalRows);
        Assert.Equal(1, Assert.Single(result.Rows).MemberCount);
        Assert.Empty((await teams.Search(1, 25, "absent", sort, descending)).Rows);
        Assert.Equal(3, (await teams.Search(1, 25, "Sample", sort, descending)).TotalRows);
        Assert.Empty(await members.CountByTeams([]));
    }
}
