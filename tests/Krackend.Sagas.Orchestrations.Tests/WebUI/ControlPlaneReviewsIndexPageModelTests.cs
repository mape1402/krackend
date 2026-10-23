using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using NSubstitute;
using ReviewsIndexModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.Reviews.IndexModel;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ControlPlaneReviewsIndexPageModelTests
{
    [Fact]
    public async Task OnGetLoadsPagedReviewsWithTheirRoadmapVersions()
    {
        var reviewService = Substitute.For<IOrchestrationVersionReviewApplicationService>();
        var versionService = Substitute.For<IOrchestrationVersionApplicationService>();
        var orchestrationService = Substitute.For<IOrchestrationApplicationService>();
        var review = CreateReview("review-1", "version-1");
        reviewService.GetAll(Arg.Any<GetAllOrchestrationVersionReviewsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ApplicationPagedResult<OrchestrationVersionReviewModel>(2, 3, 51, 25, [review])));
        versionService.GetById(Arg.Any<GetOrchestrationVersionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new OrchestrationVersionModel
            {
                Id = "version-1",
                OrchestrationDefinitionId = "orchestration-1",
                Version = "1.4.0",
                Status = OrchestrationVersionStatus.Deployed,
            }));
        orchestrationService.GetById(Arg.Any<GetOrchestrationDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new OrchestrationDefinitionModel
            {
                Id = "orchestration-1",
                Key = "sales_sale_created",
                Name = "Sales sale created",
            }));
        var page = new ReviewsIndexModel(reviewService, versionService, orchestrationService);

        await page.OnGetAsync(2);

        var row = Assert.Single(page.Rows);
        Assert.Equal("review-1", row.Review.Id);
        Assert.Equal("1.4.0", row.Version.Version);
        Assert.Equal("Sales sale created", row.Orchestration.Name);
        Assert.Equal("sales_sale_created", row.Orchestration.Key);
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(3, page.TotalPages);
        await reviewService.Received(1).GetAll(
            Arg.Is<GetAllOrchestrationVersionReviewsQuery>(x => x.Settings.PageNumber == 2 && x.Settings.PageSize == 25),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnGetKeepsTheReviewVisibleWhenItsRoadmapVersionCannotBeFound()
    {
        var reviewService = Substitute.For<IOrchestrationVersionReviewApplicationService>();
        var versionService = Substitute.For<IOrchestrationVersionApplicationService>();
        var orchestrationService = Substitute.For<IOrchestrationApplicationService>();
        var review = CreateReview("review-missing", "version-missing");
        reviewService.GetAll(Arg.Any<GetAllOrchestrationVersionReviewsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ApplicationPagedResult<OrchestrationVersionReviewModel>(1, 1, 1, 25, [review])));
        versionService.GetById(Arg.Any<GetOrchestrationVersionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<OrchestrationVersionModel>(new KeyNotFoundException()));
        var page = new ReviewsIndexModel(reviewService, versionService, orchestrationService);

        await page.OnGetAsync();

        var row = Assert.Single(page.Rows);
        Assert.Equal("review-missing", row.Review.Id);
        Assert.Null(row.Version);
        Assert.Equal(string.Empty, page.ErrorMessage);
    }

    [Fact]
    public async Task OnGetUsesFriendlyErrorWhenTheReviewQueueFails()
    {
        var reviewService = Substitute.For<IOrchestrationVersionReviewApplicationService>();
        reviewService.GetAll(Arg.Any<GetAllOrchestrationVersionReviewsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ApplicationPagedResult<OrchestrationVersionReviewModel>>(new InvalidOperationException("storage unavailable")));
        var page = new ReviewsIndexModel(
            reviewService,
            Substitute.For<IOrchestrationVersionApplicationService>(),
            Substitute.For<IOrchestrationApplicationService>());

        await page.OnGetAsync(0);

        Assert.Empty(page.Rows);
        Assert.Equal(1, page.PageNumber);
        Assert.Equal("Lifecycle reviews could not be loaded. Try again in a moment.", page.ErrorMessage);
    }

    [Fact]
    public void ConstructorRejectsMissingDependencies()
    {
        var reviews = Substitute.For<IOrchestrationVersionReviewApplicationService>();
        var versions = Substitute.For<IOrchestrationVersionApplicationService>();
        var orchestrations = Substitute.For<IOrchestrationApplicationService>();

        Assert.Equal("reviewService", Assert.Throws<ArgumentNullException>(() => new ReviewsIndexModel(null!, versions, orchestrations)).ParamName);
        Assert.Equal("versionService", Assert.Throws<ArgumentNullException>(() => new ReviewsIndexModel(reviews, null!, orchestrations)).ParamName);
        Assert.Equal("orchestrationService", Assert.Throws<ArgumentNullException>(() => new ReviewsIndexModel(reviews, versions, null!)).ParamName);
    }

    private static OrchestrationVersionReviewModel CreateReview(string id, string versionId)
        => new()
        {
            Id = id,
            OrchestrationVersionId = versionId,
            Operation = OrchestrationVersionReviewOperation.Deploy,
            ApprovalStatus = OrchestrationVersionReviewApprovalStatus.Pending,
            ApplicationStatus = OrchestrationVersionReviewApplicationStatus.Pending,
            RequestedBy = "designer",
            RequestedOnUtc = DateTime.UtcNow,
        };
}
