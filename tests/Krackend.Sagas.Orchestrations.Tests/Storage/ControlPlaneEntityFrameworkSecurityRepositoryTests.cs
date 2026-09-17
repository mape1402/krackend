using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class ControlPlaneEntityFrameworkSecurityRepositoryTests
{
    [Fact]
    public async Task TeamRepositoryPersistsUpdatesSearchesAndPagesTeams()
    {
        await using var fixture = CreateProvider();
        using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITeamRepository>();
        var alpha = Team("alpha", "Alpha Team", "Handles alpha workflows");
        var beta = Team("beta", "Beta Team", null);
        var gamma = Team("gamma", "Gamma Team", "Search target");

        await repository.Upsert(beta);
        await repository.Upsert(gamma);
        await repository.Upsert(alpha);
        alpha.DisplayName = "Alpha Team Updated";
        alpha.Description = "Updated description";
        alpha.UpdatedOnUtc = DateTime.UtcNow;
        await repository.Upsert(alpha);
        await repository.SetIsActive(beta.Id, false);

        var all = await repository.GetAll(pageNumber: 0, pageSize: 0);
        var search = await repository.GetAll(pageNumber: 1, pageSize: 5, searchText: "target");
        var byId = await repository.GetById(alpha.Id);
        var byKey = await repository.GetByKey("alpha");

        Assert.Equal(1, all.PageNumber);
        Assert.Equal(25, all.PageSize);
        Assert.Equal(3, all.TotalRows);
        Assert.Equal(["Alpha Team Updated", "Beta Team", "Gamma Team"], all.Rows.Select(x => x.DisplayName).ToArray());
        Assert.Single(search.Rows);
        Assert.Equal(gamma.Id, search.Rows.Single().Id);
        Assert.Equal("Updated description", byId.Description);
        Assert.Equal(byId.Id, byKey.Id);
        Assert.False((await repository.GetById(beta.Id)).IsActive);
        Assert.Null(await repository.GetByKey(""));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => repository.GetById(Id.New()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => repository.SetIsActive(Id.New(), true));
    }

    [Fact]
    public async Task TeamMemberRepositoryAddsQueriesAndRemovesMembers()
    {
        await using var fixture = CreateProvider();
        using var scope = fixture.CreateScope();
        var teamRepository = scope.ServiceProvider.GetRequiredService<ITeamRepository>();
        var memberRepository = scope.ServiceProvider.GetRequiredService<ITeamMemberRepository>();
        var team = Team("ops", "Operations", "Operations team");
        await teamRepository.Upsert(team);
        var mario = Member(team.Id, "mario", "Mario");
        var luisa = Member(team.Id, "luisa", "Luisa");

        await memberRepository.Add(mario);
        await memberRepository.Add(luisa);
        await memberRepository.Remove(team.Id, "missing");

        Assert.True(await memberRepository.Exists(team.Id, "mario"));
        Assert.False(await memberRepository.Exists(team.Id, "missing"));
        Assert.Equal(["luisa", "mario"], (await memberRepository.GetByTeam(team.Id)).Select(x => x.ExternalUserId).OrderBy(x => x).ToArray());

        await memberRepository.Remove(team.Id, "mario");

        Assert.False(await memberRepository.Exists(team.Id, "mario"));
        var remaining = Assert.Single(await memberRepository.GetByTeam(team.Id));
        Assert.Equal("luisa", remaining.ExternalUserId);
        Assert.Equal("Luisa", remaining.DisplayName);
    }

    private static Team Team(string key, string displayName, string? description)
        => new()
        {
            Id = Id.New(),
            Key = key,
            DisplayName = displayName,
            Description = description,
            IsActive = true,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow,
        };

    private static TeamMember Member(Id teamId, string externalUserId, string displayName)
        => new()
        {
            Id = Id.New(),
            TeamId = teamId,
            ExternalUserId = externalUserId,
            DisplayName = displayName,
            CreatedOnUtc = DateTime.UtcNow,
        };

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<Sieve.Models.SieveOptions>(_ => { });
        services.AddOrchestratorControlPlaneStorageEntityFramework(options =>
            options.UseInMemoryDatabase($"control-plane-security-{Guid.NewGuid():N}"));
        return services.BuildServiceProvider();
    }
}
