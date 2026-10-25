using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using AssignmentsIndexModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Assignments.IndexModel;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ControlPlaneAssignmentsPageModelTests
{
    [Fact]
    public async Task OnGetLoadsOnlyAttemptsWhenAssignmentIsSelected()
    {
        var service = Substitute.For<IReleaseTargetApplicationService>();
        var target = new ReleaseTargetModel
        {
            Id = Ulid.NewUlid().ToString(),
            RuntimeNodeId = "runtime-1",
            ArtifactId = "artifact-1",
            ReleaseId = "release-1",
            RolloutGroup = "default",
            Status = "Queued",
            ActivationStatus = "Pending",
            AssignedAtUtc = DateTime.UtcNow
        };
        var attempt = new ReleaseAttemptModel
        {
            Id = "attempt-1",
            ReleaseTargetId = target.Id,
            Action = "Push",
            InitiatedBy = "tests",
            StartedAtUtc = DateTime.UtcNow,
            Succeeded = true
        };
        service.GetAttempts(target.Id, Arg.Any<CancellationToken>()).Returns([attempt]);
        var page = new AssignmentsIndexModel(service) { AssignmentId = target.Id };

        await page.OnGetAsync();

        Assert.Same(attempt, page.Attempts.Single());
        await service.DidNotReceive().GetAll(Arg.Any<ApplicationPagedSettings>(), Arg.Any<CancellationToken>());
        await service.Received(1).GetAttempts(target.Id, Arg.Any<CancellationToken>());
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
