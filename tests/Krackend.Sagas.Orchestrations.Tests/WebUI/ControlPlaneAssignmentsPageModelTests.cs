using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using AssignmentsIndexModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Assignments.IndexModel;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ControlPlaneAssignmentsPageModelTests
{
    [Fact]
    public async Task OnGetDoesNotLoadAttemptsBeforeTheGridRequestsThem()
    {
        var service = Substitute.For<IReleaseTargetApplicationService>();
        var id = Ulid.NewUlid().ToString();
        var page = new AssignmentsIndexModel(service) { AssignmentId = id };

        await page.OnGetAsync();

        await service.DidNotReceive().GetAll(Arg.Any<ApplicationPagedSettings>(), Arg.Any<CancellationToken>());
        await service.DidNotReceive().GetAttempts(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await service.DidNotReceive().SearchAttempts(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AttemptListForwardsSearchAndBoundedPage()
    {
        var service = Substitute.For<IReleaseTargetApplicationService>();
        var id = Ulid.NewUlid().ToString();
        service.SearchAttempts(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new ApplicationPagedResult<ReleaseAttemptModel> { PageNumber = 1, PageSize = 100, Rows = [] });
        var page = new AssignmentsIndexModel(service);

        Assert.IsType<JsonResult>(await page.OnGetAttemptListAsync(id, 0, 500, " failed ", descending: true));
        await service.Received(1).SearchAttempts(id, 1, 100, "failed", true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AttemptListRejectsInvalidAssignmentId()
    {
        var service = Substitute.For<IReleaseTargetApplicationService>();
        var page = new AssignmentsIndexModel(service);

        Assert.IsType<BadRequestObjectResult>(await page.OnGetAttemptListAsync("invalid"));
        await service.DidNotReceive().SearchAttempts(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListForwardsSearchSortingAndBoundedPage()
    {
        var service = Substitute.For<IReleaseTargetApplicationService>();
        service.Search(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new ApplicationPagedResult<ReleaseTargetModel> { PageNumber = 2, PageSize = 100, Rows = [] });
        var page = new AssignmentsIndexModel(service) { AssignmentId = "invalid" };
        await page.OnGetAsync();
        Assert.IsType<JsonResult>(await page.OnGetListAsync(2, 500, " Node ", "status", true));
        await service.Received(1).Search(2, 100, "Node", "status", true, Arg.Any<CancellationToken>());
        await service.DidNotReceive().GetAttempts(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
