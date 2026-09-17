namespace Krackend.Sagas.Orchestrations.Tests.Design;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;
using NSubstitute;

public sealed class ControlPlaneSecurityApplicationTests
{
    [Fact]
    public async Task UpsertTeamHandlerCreatesAndUpdatesTeamsWhileGuardingDuplicateKeys()
    {
        var repository = Substitute.For<ITeamRepository>();
        Team? upserted = null;
        repository.Upsert(Arg.Do<Team>(model => upserted = model), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var handler = new UpsertTeamCommandHandler(repository);

        var createdId = await handler.Handle(
            new UpsertTeamCommand(
                "",
                " sales ",
                " Sales Team ",
                "  Owns sale orchestration  ",
                "operator"),
            CancellationToken.None);

        Assert.Equal(createdId, upserted!.Id.ToString());
        Assert.Equal("sales", upserted.Key);
        Assert.Equal("Sales Team", upserted.DisplayName);
        Assert.Equal("Owns sale orchestration", upserted.Description);
        Assert.True(upserted.IsActive);
        Assert.Null(upserted.UpdatedOnUtc);

        var existing = upserted;
        existing.IsActive = false;
        repository.GetById(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        repository.GetByKey("sales", Arg.Any<CancellationToken>()).Returns(existing);

        var updatedId = await handler.Handle(
            new UpsertTeamCommand(
                existing.Id.ToString(),
                " sales ",
                " Sales Ops ",
                null!,
                "operator"),
            CancellationToken.None);

        Assert.Equal(existing.Id.ToString(), updatedId);
        Assert.Equal("Sales Ops", upserted.DisplayName);
        Assert.Equal(string.Empty, upserted.Description);
        Assert.False(upserted.IsActive);
        Assert.Equal(existing.CreatedOnUtc, upserted.CreatedOnUtc);
        Assert.NotNull(upserted.UpdatedOnUtc);

        repository.GetByKey("sales", Arg.Any<CancellationToken>())
            .Returns(new Team { Id = Id.New(), Key = "sales", DisplayName = "Other" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new UpsertTeamCommand(existing.Id.ToString(), "sales", "Duplicate", "", "operator"),
            CancellationToken.None));
    }

    [Fact]
    public async Task TeamMemberHandlersAddRemoveAndSortMembers()
    {
        var team = new Team
        {
            Id = Id.New(),
            Key = "sales",
            DisplayName = "Sales",
            IsActive = true
        };
        var teamRepository = Substitute.For<ITeamRepository>();
        var memberRepository = Substitute.For<ITeamMemberRepository>();
        TeamMember? added = null;
        teamRepository.GetById(team.Id, Arg.Any<CancellationToken>()).Returns(team);
        memberRepository.Exists(team.Id, "user-1", Arg.Any<CancellationToken>()).Returns(false);
        memberRepository.Add(Arg.Do<TeamMember>(model => added = model), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var addHandler = new AddTeamMemberCommandHandler(teamRepository, memberRepository);

        var addedResult = await addHandler.Handle(
            new AddTeamMemberCommand(team.Id.ToString(), " user-1 ", " User One "),
            CancellationToken.None);

        Assert.True(addedResult);
        Assert.NotNull(added);
        Assert.Equal(team.Id, added.TeamId);
        Assert.Equal("user-1", added.ExternalUserId);
        Assert.Equal("User One", added.DisplayName);

        memberRepository.Exists(team.Id, "user-1", Arg.Any<CancellationToken>()).Returns(true);
        Assert.True(await addHandler.Handle(
            new AddTeamMemberCommand(team.Id.ToString(), " user-1 ", "Ignored"),
            CancellationToken.None));
        await memberRepository.Received(1).Add(Arg.Any<TeamMember>(), Arg.Any<CancellationToken>());

        team.IsActive = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => addHandler.Handle(
            new AddTeamMemberCommand(team.Id.ToString(), "user-2", "User Two"),
            CancellationToken.None));

        var removeHandler = new RemoveTeamMemberCommandHandler(memberRepository);
        Assert.True(await removeHandler.Handle(
            new RemoveTeamMemberCommand(team.Id.ToString(), " user-1 "),
            CancellationToken.None));
        await memberRepository.Received(1).Remove(team.Id, "user-1", Arg.Any<CancellationToken>());

        memberRepository.GetByTeam(team.Id, Arg.Any<CancellationToken>()).Returns([
            new TeamMember { Id = Id.New(), TeamId = team.Id, ExternalUserId = "z-user", DisplayName = "Zed", CreatedOnUtc = DateTime.UtcNow },
            new TeamMember { Id = Id.New(), TeamId = team.Id, ExternalUserId = "a-user", DisplayName = null, CreatedOnUtc = DateTime.UtcNow },
            new TeamMember { Id = Id.New(), TeamId = team.Id, ExternalUserId = "b-user", DisplayName = "Alpha", CreatedOnUtc = DateTime.UtcNow }
        ]);
        var members = await new GetTeamMembersQueryHandler(memberRepository).Handle(
            new GetTeamMembersQuery(team.Id.ToString()),
            CancellationToken.None);

        Assert.Equal(["a-user", "b-user", "z-user"], members.Select(x => x.ExternalUserId).ToArray());
        Assert.Equal(string.Empty, members.First().DisplayName);
        Assert.All(members, member => Assert.Equal(team.Id.ToString(), member.TeamId));
    }

    [Fact]
    public async Task SetTeamIsActiveHandlerDelegatesStateChange()
    {
        var teamId = Id.New();
        var repository = Substitute.For<ITeamRepository>();
        var handler = new SetTeamIsActiveCommandHandler(repository);

        Assert.True(await handler.Handle(
            new SetTeamIsActiveCommand(teamId.ToString(), false, "operator"),
            CancellationToken.None));

        await repository.Received(1).SetIsActive(teamId, false, Arg.Any<CancellationToken>());
    }
}
