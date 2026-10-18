using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using ReviewDetailsModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.Reviews.DetailsModel;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ControlPlaneReviewDetailsPageModelTests
{
    [Fact]
    public async Task OnGetLoadsTheReviewAndItsRoadmapVersion()
    {
        var context = CreateContext();

        var result = await context.Page.OnGetAsync("review-1");

        Assert.IsType<PageResult>(result);
        Assert.Equal("review-1", context.Page.ReviewId);
        Assert.Equal("version-1", context.Page.Review.OrchestrationVersionId);
        Assert.Equal("orchestration-1", context.Page.OrchestrationId);
        Assert.True(context.Page.CanApprove);
        Assert.False(context.Page.CanApply);
        Assert.True(context.Page.CanAbort);
    }

    [Fact]
    public async Task ReviewActionAvailabilityFollowsApprovalAndApplicationState()
    {
        var context = CreateContext();
        var review = context.Review;

        Assert.False(context.Page.CanApprove);
        Assert.False(context.Page.CanApply);
        Assert.False(context.Page.CanAbort);

        await context.Page.OnGetAsync("review-1");
        Assert.True(context.Page.CanApprove);
        Assert.False(context.Page.CanApply);
        Assert.True(context.Page.CanAbort);

        review.ApprovalStatus = OrchestrationVersionReviewApprovalStatus.Approved;
        await context.Page.OnGetAsync("review-1");
        Assert.False(context.Page.CanApprove);
        Assert.True(context.Page.CanApply);
        Assert.True(context.Page.CanAbort);

        review.ApplicationStatus = OrchestrationVersionReviewApplicationStatus.Applied;
        await context.Page.OnGetAsync("review-1");
        Assert.False(context.Page.CanApprove);
        Assert.False(context.Page.CanApply);
        Assert.False(context.Page.CanAbort);

        review.ApplicationStatus = OrchestrationVersionReviewApplicationStatus.Pending;
        review.ApprovalStatus = OrchestrationVersionReviewApprovalStatus.Rejected;
        await context.Page.OnGetAsync("review-1");
        Assert.False(context.Page.CanApprove);
        Assert.False(context.Page.CanApply);
        Assert.False(context.Page.CanAbort);
    }

    [Fact]
    public async Task OnGetReturnsNotFoundWhenTheReviewDoesNotExist()
    {
        var context = CreateContext();
        context.ReviewService.GetById(Arg.Any<GetOrchestrationVersionReviewByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<OrchestrationVersionReviewModel>(null!));

        var result = await context.Page.OnGetAsync("missing");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGetReturnsNotFoundWhenTheRoadmapVersionDoesNotExist()
    {
        var context = CreateContext();
        context.VersionService.GetById(Arg.Any<GetOrchestrationVersionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<OrchestrationVersionModel>(null!));

        var result = await context.Page.OnGetAsync("review-1");

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(string.Empty, context.Page.OrchestrationId);
    }

    [Fact]
    public async Task OnGetUsesFriendlyMessagesWhenReviewLoadingFails()
    {
        var missing = CreateContext();
        missing.ReviewService.GetById(Arg.Any<GetOrchestrationVersionReviewByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<OrchestrationVersionReviewModel>(new KeyNotFoundException()));

        var missingResult = await missing.Page.OnGetAsync("review-1");

        var unavailable = CreateContext();
        unavailable.ReviewService.GetById(Arg.Any<GetOrchestrationVersionReviewByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<OrchestrationVersionReviewModel>(new InvalidOperationException()));

        var unavailableResult = await unavailable.Page.OnGetAsync("review-1");

        Assert.IsType<NotFoundResult>(missingResult);
        Assert.Equal("The requested lifecycle review no longer exists.", missing.Page.ErrorMessage);
        Assert.IsType<NotFoundResult>(unavailableResult);
        Assert.Equal("The lifecycle review could not be loaded.", unavailable.Page.ErrorMessage);
    }

    [Fact]
    public async Task ApprovePostsTheReviewDecisionAndRedirectsBackToTheReview()
    {
        var context = CreateContext();

        var result = await context.Page.OnPostApproveAsync("review-1");

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Reviews/Details", redirect.PageName);
        await context.ReviewService.Received(1).Approve(
            Arg.Is<ApproveOrchestrationVersionReviewCommand>(x => x.ReviewId == "review-1" && x.ApprovedBy == "web-ui"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectPostsTheOptionalCommentAndRedirectsBackToTheReview()
    {
        var context = CreateContext();
        context.Page.RejectionComment = "Needs more rollout evidence.";

        var result = await context.Page.OnPostRejectAsync("review-1");

        Assert.IsType<RedirectToPageResult>(result);
        await context.ReviewService.Received(1).Reject(
            Arg.Is<RejectOrchestrationVersionReviewCommand>(x =>
                x.ReviewId == "review-1" &&
                x.RejectedBy == "web-ui" &&
                x.Comment == "Needs more rollout evidence."),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AbortPostsTheOptionalReasonAndRedirectsBackToTheReview()
    {
        var context = CreateContext();
        context.Page.AbortReason = "Superseded by a newer draft.";

        var result = await context.Page.OnPostAbortAsync("review-1");

        Assert.IsType<RedirectToPageResult>(result);
        await context.ReviewService.Received(1).Abort(
            Arg.Is<AbortOrchestrationVersionReviewCommand>(x =>
                x.ReviewId == "review-1" &&
                x.AbortedBy == "web-ui" &&
                x.Reason == "Superseded by a newer draft."),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyRejectsAnInvalidSemanticVersionBeforeChangingTheReview()
    {
        var context = CreateContext();
        context.Page.ApplyInput = new ReviewDetailsModel.DeploymentInput { Version = "1.x.0" };

        var result = await context.Page.OnPostApplyAsync("review-1");

        Assert.IsType<PageResult>(result);
        Assert.Equal("Use semantic version format, for example 1.0.0.", context.Page.ModelState[nameof(ReviewDetailsModel.DeploymentInput.Version)]!.Errors.Single().ErrorMessage);
        await context.ReviewService.DidNotReceive().Apply(Arg.Any<ApplyOrchestrationVersionReviewCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyRequiresAVersionWhenDeployingADraft()
    {
        var context = CreateContext();
        context.Page.ApplyInput = new ReviewDetailsModel.DeploymentInput();

        var result = await context.Page.OnPostApplyAsync("review-1");

        Assert.IsType<PageResult>(result);
        Assert.Equal("A version is required to deploy a draft.", context.Page.ModelState[nameof(ReviewDetailsModel.DeploymentInput.Version)]!.Errors.Single().ErrorMessage);
        await context.ReviewService.DidNotReceive().Apply(Arg.Any<ApplyOrchestrationVersionReviewCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyDeploysAnApprovedDraftWithItsReleaseDetails()
    {
        var context = CreateContext();
        context.Page.ApplyInput = new ReviewDetailsModel.DeploymentInput
        {
            Version = "1.4.0",
            VersionLabel = "Autumn release",
            Description = "Adds the delivery branch.",
            Notes = "Approved after a production rehearsal."
        };

        var result = await context.Page.OnPostApplyAsync("review-1");

        Assert.IsType<RedirectToPageResult>(result);
        await context.ReviewService.Received(1).Apply(
            Arg.Is<ApplyOrchestrationVersionReviewCommand>(x =>
                x.ReviewId == "review-1" &&
                x.AppliedBy == "web-ui" &&
                x.Version == "1.4.0" &&
                x.VersionLabel == "Autumn release" &&
                x.Description == "Adds the delivery branch." &&
                x.Notes == "Approved after a production rehearsal."),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyDoesNotRequireAVersionForDeprecationOrArchivalActions()
    {
        var context = CreateContext(operation: OrchestrationVersionReviewOperation.Deprecate);
        context.Page.ApplyInput = new ReviewDetailsModel.DeploymentInput();

        var result = await context.Page.OnPostApplyAsync("review-1");

        Assert.IsType<RedirectToPageResult>(result);
        await context.ReviewService.Received(1).Apply(
            Arg.Is<ApplyOrchestrationVersionReviewCommand>(x => x.Version == string.Empty),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MutationsKeepInvalidOperationMessagesAndHideUnexpectedFailures()
    {
        var invalid = CreateContext();
        invalid.ReviewService.Approve(Arg.Any<ApproveOrchestrationVersionReviewCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new InvalidOperationException("The review was already decided.")));

        var invalidResult = await invalid.Page.OnPostApproveAsync("review-1");

        var unexpected = CreateContext();
        unexpected.ReviewService.Abort(Arg.Any<AbortOrchestrationVersionReviewCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new Exception("hidden")));

        var unexpectedResult = await unexpected.Page.OnPostAbortAsync("review-1");

        Assert.IsType<PageResult>(invalidResult);
        Assert.Equal("The review was already decided.", invalid.Page.ErrorMessage);
        Assert.IsType<PageResult>(unexpectedResult);
        Assert.Equal("The lifecycle review could not be updated. Review the data and try again.", unexpected.Page.ErrorMessage);
    }

    [Fact]
    public void ConstructorRejectsMissingServices()
    {
        var reviews = Substitute.For<IOrchestrationVersionReviewApplicationService>();
        var versions = Substitute.For<IOrchestrationVersionApplicationService>();

        Assert.Equal("reviewService", Assert.Throws<ArgumentNullException>(() => new ReviewDetailsModel(null!, versions)).ParamName);
        Assert.Equal("versionService", Assert.Throws<ArgumentNullException>(() => new ReviewDetailsModel(reviews, null!)).ParamName);
    }

    private static TestContext CreateContext(OrchestrationVersionReviewOperation operation = OrchestrationVersionReviewOperation.Deploy)
    {
        var reviewService = Substitute.For<IOrchestrationVersionReviewApplicationService>();
        var versionService = Substitute.For<IOrchestrationVersionApplicationService>();
        var review = new OrchestrationVersionReviewModel
        {
            Id = "review-1",
            OrchestrationVersionId = "version-1",
            Operation = operation,
            ApprovalStatus = operation == OrchestrationVersionReviewOperation.Deploy
                ? OrchestrationVersionReviewApprovalStatus.Pending
                : OrchestrationVersionReviewApprovalStatus.Approved,
            ApplicationStatus = OrchestrationVersionReviewApplicationStatus.Pending,
            RequestedBy = "designer",
            RequestedOnUtc = DateTime.UtcNow
        };

        reviewService.GetById(Arg.Any<GetOrchestrationVersionReviewByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(review));
        reviewService.Approve(Arg.Any<ApproveOrchestrationVersionReviewCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        reviewService.Reject(Arg.Any<RejectOrchestrationVersionReviewCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        reviewService.Abort(Arg.Any<AbortOrchestrationVersionReviewCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        reviewService.Apply(Arg.Any<ApplyOrchestrationVersionReviewCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        versionService.GetById(Arg.Any<GetOrchestrationVersionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new OrchestrationVersionModel
            {
                Id = "version-1",
                OrchestrationDefinitionId = "orchestration-1",
                Version = "1.3.0",
                Status = OrchestrationVersionStatus.Draft
            }));

        return new TestContext(new ReviewDetailsModel(reviewService, versionService), reviewService, versionService, review);
    }

    private sealed record TestContext(
        ReviewDetailsModel Page,
        IOrchestrationVersionReviewApplicationService ReviewService,
        IOrchestrationVersionApplicationService VersionService,
        OrchestrationVersionReviewModel Review);
}
