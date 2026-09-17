using Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TeamsIndexModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Security.Areas.OrchestratorSecurity.Pages.Teams.IndexModel;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ControlPlaneTeamsPageModelTests
{
    [Fact]
    public void ConstructorRejectsMissingService()
    {
        Assert.Throws<ArgumentNullException>(() => new TeamsIndexModel(null!));
    }

    [Fact]
    public async Task OnGetLoadsTeamsAndSelectedMembers()
    {
        var service = new FakeTeamApplicationService();
        var page = new TeamsIndexModel(service) { TeamId = "team-1" };

        await page.OnGetAsync();

        Assert.Single(page.Rows);
        Assert.Single(page.TeamMembers);
        Assert.Equal("team-1", service.LastMembersQuery.TeamId);
    }

    [Fact]
    public async Task UpsertTrimsInputAndReturnsPageWhenModelStateIsInvalid()
    {
        var service = new FakeTeamApplicationService();
        var page = new TeamsIndexModel(service);
        page.Input.Key = " sales-team ";
        page.Input.DisplayName = " Sales Team ";
        page.Input.Description = " Owns sales ";

        var success = await page.OnPostUpsertAsync();

        Assert.IsType<RedirectToPageResult>(success);
        Assert.Equal("sales-team", service.LastUpsert.Key);
        Assert.Equal("Sales Team", service.LastUpsert.DisplayName);
        Assert.Equal("Owns sales", service.LastUpsert.Description);
        Assert.Equal("web-ui", service.LastUpsert.Actor);

        page.ModelState.AddModelError("Input.Key", "Key is required.");
        var invalid = await page.OnPostUpsertAsync();

        Assert.IsType<PageResult>(invalid);
        Assert.Single(page.Rows);
    }

    [Fact]
    public async Task SetActiveAndRemoveMemberDelegateAndRedirect()
    {
        var service = new FakeTeamApplicationService();
        var page = new TeamsIndexModel(service);

        var active = await page.OnPostSetIsActiveAsync("team-1", false);
        var remove = await page.OnPostRemoveMemberAsync("team-1", "user-1");

        Assert.IsType<RedirectToPageResult>(active);
        Assert.False(service.LastSetIsActive.IsActive);
        Assert.Equal("web-ui", service.LastSetIsActive.Actor);
        var redirect = Assert.IsType<RedirectToPageResult>(remove);
        Assert.Equal("team-1", redirect.RouteValues!["teamId"]);
        Assert.Equal("user-1", service.LastRemoveMember.ExternalUserId);
    }

    [Fact]
    public async Task AddMemberTrimsInputAndReloadsSelectedTeamWhenInvalid()
    {
        var service = new FakeTeamApplicationService();
        var page = new TeamsIndexModel(service);
        page.MemberInput.TeamId = "team-1";
        page.MemberInput.ExternalUserId = " user-1 ";
        page.MemberInput.DisplayName = " User One ";

        var success = await page.OnPostAddMemberAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(success);
        Assert.Equal("team-1", redirect.RouteValues!["teamId"]);
        Assert.Equal("user-1", service.LastAddMember.ExternalUserId);
        Assert.Equal("User One", service.LastAddMember.DisplayName);

        page.ModelState.AddModelError("MemberInput.ExternalUserId", "User is required.");
        var invalid = await page.OnPostAddMemberAsync();

        Assert.IsType<PageResult>(invalid);
        Assert.Equal("team-1", page.TeamId);
        Assert.Single(page.TeamMembers);
    }

    [Fact]
    public async Task MembersEndpointReturnsJsonRows()
    {
        var service = new FakeTeamApplicationService();
        var page = new TeamsIndexModel(service);

        var result = await page.OnGetMembersAsync("team-1");

        var json = Assert.IsType<JsonResult>(result);
        var rows = Assert.IsAssignableFrom<IReadOnlyCollection<TeamMemberModel>>(json.Value);
        Assert.Single(rows);
    }

    private sealed class FakeTeamApplicationService : ITeamApplicationService
    {
        public UpsertTeamCommand LastUpsert { get; private set; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

        public SetTeamIsActiveCommand LastSetIsActive { get; private set; } = new(string.Empty, true, string.Empty);

        public AddTeamMemberCommand LastAddMember { get; private set; } = new(string.Empty, string.Empty, string.Empty);

        public RemoveTeamMemberCommand LastRemoveMember { get; private set; } = new(string.Empty, string.Empty);

        public GetTeamMembersQuery LastMembersQuery { get; private set; } = new(string.Empty);

        public Task<string> Upsert(UpsertTeamCommand command, CancellationToken cancellationToken = default)
        {
            LastUpsert = command;
            return Task.FromResult("team-1");
        }

        public Task<bool> SetIsActive(SetTeamIsActiveCommand command, CancellationToken cancellationToken = default)
        {
            LastSetIsActive = command;
            return Task.FromResult(true);
        }

        public Task<bool> AddMember(AddTeamMemberCommand command, CancellationToken cancellationToken = default)
        {
            LastAddMember = command;
            return Task.FromResult(true);
        }

        public Task<bool> RemoveMember(RemoveTeamMemberCommand command, CancellationToken cancellationToken = default)
        {
            LastRemoveMember = command;
            return Task.FromResult(true);
        }

        public Task<ApplicationPagedResult<TeamModel>> GetAll(GetTeamsQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(new ApplicationPagedResult<TeamModel>(
                1,
                1,
                1,
                query.PagedSettings.PageSize,
                [
                    new TeamModel
                    {
                        Id = "team-1",
                        Key = "sales",
                        DisplayName = "Sales",
                        IsActive = true,
                        MemberCount = 1
                    }
                ]));

        public Task<IReadOnlyCollection<TeamMemberModel>> GetMembers(GetTeamMembersQuery query, CancellationToken cancellationToken = default)
        {
            LastMembersQuery = query;
            return Task.FromResult<IReadOnlyCollection<TeamMemberModel>>(
            [
                new TeamMemberModel
                {
                    Id = "member-1",
                    TeamId = query.TeamId,
                    ExternalUserId = "user-1",
                    DisplayName = "User One",
                    CreatedOnUtc = "2026-08-20T12:00:00Z"
                }
            ]);
        }
    }
}
