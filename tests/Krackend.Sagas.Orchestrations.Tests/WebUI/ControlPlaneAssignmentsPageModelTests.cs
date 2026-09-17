using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using NSubstitute;
using AssignmentsIndexModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Assignments.IndexModel;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ControlPlaneAssignmentsPageModelTests
{
    [Fact]
    public async Task OnGetLoadsAssignmentsAndAttemptsWhenAssignmentIsSelected()
    {
        var service = Substitute.For<IReleaseTargetApplicationService>();
        var target = new ReleaseTargetModel
        {
            Id = "target-1",
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
        service
            .GetAll(Arg.Any<ApplicationPagedSettings>(), Arg.Any<CancellationToken>())
            .Returns(new ApplicationPagedResult<ReleaseTargetModel> { Rows = [target], TotalRows = 1 });
        service.GetAttempts(target.Id, Arg.Any<CancellationToken>()).Returns([attempt]);
        var page = new AssignmentsIndexModel(service) { AssignmentId = target.Id };

        await page.OnGetAsync();

        Assert.Same(target, page.Rows.Single());
        Assert.Same(attempt, page.Attempts.Single());
        await service.Received(1).GetAll(
            Arg.Is<ApplicationPagedSettings>(settings => settings.PageNumber == 1 && settings.PageSize == 200),
            Arg.Any<CancellationToken>());
        await service.Received(1).GetAttempts(target.Id, Arg.Any<CancellationToken>());
    }
}
