using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Abstractions;
using NSubstitute;
using ReviewsIndexModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.Reviews.IndexModel;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ControlPlaneReviewsIndexPageModelTests
{
    [Fact]
    public async Task ListLoadsOnlyRequestedPageAndForwardsSearchAndSort()
    {
        var reviews = Substitute.For<IOrchestrationVersionReviewApplicationService>();
        var versions = Substitute.For<IOrchestrationVersionApplicationService>();
        var orchestrations = Substitute.For<IOrchestrationApplicationService>();
        reviews.GetAll(Arg.Any<GetAllOrchestrationVersionReviewsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ApplicationPagedResult<OrchestrationVersionReviewModel>(2, 3, 51, 25, [CreateReview()]));
        versions.GetById(Arg.Any<GetOrchestrationVersionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(new OrchestrationVersionModel { OrchestrationDefinitionId = "orchestration-1", Version = "1.4.0" });
        orchestrations.GetById(Arg.Any<GetOrchestrationDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(new OrchestrationDefinitionModel { Name = "Sales", Key = "sales" });
        var page = CreatePage(reviews, versions, orchestrations);

        page.OnGet();
        await reviews.DidNotReceive().GetAll(Arg.Any<GetAllOrchestrationVersionReviewsQuery>(), Arg.Any<CancellationToken>());
        var result = Assert.IsType<JsonResult>(await page.OnGetListAsync(2, 500, " sale ", "operation", true));

        await reviews.Received(1).GetAll(Arg.Is<GetAllOrchestrationVersionReviewsQuery>(x =>
            x.Settings.PageNumber == 2 && x.Settings.PageSize == 100 && x.SearchText == "sale" && x.SortField == "operation" && x.Descending), Arg.Any<CancellationToken>());
        Assert.Contains("Sales", System.Text.Json.JsonSerializer.Serialize(result.Value));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingRelatedEntitiesDoNotHideReview(bool missingVersion)
    {
        var reviews = Substitute.For<IOrchestrationVersionReviewApplicationService>();
        var versions = Substitute.For<IOrchestrationVersionApplicationService>();
        var orchestrations = Substitute.For<IOrchestrationApplicationService>();
        reviews.GetAll(Arg.Any<GetAllOrchestrationVersionReviewsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ApplicationPagedResult<OrchestrationVersionReviewModel>(1, 1, 1, 25, [CreateReview()]));
        if (missingVersion)
            versions.GetById(Arg.Any<GetOrchestrationVersionByIdQuery>(), Arg.Any<CancellationToken>()).Returns<Task<OrchestrationVersionModel>>(_ => throw new KeyNotFoundException());
        else
        {
            versions.GetById(Arg.Any<GetOrchestrationVersionByIdQuery>(), Arg.Any<CancellationToken>()).Returns(new OrchestrationVersionModel { OrchestrationDefinitionId = "missing" });
            orchestrations.GetById(Arg.Any<GetOrchestrationDefinitionByIdQuery>(), Arg.Any<CancellationToken>()).Returns<Task<OrchestrationDefinitionModel>>(_ => throw new KeyNotFoundException());
        }
        var page = CreatePage(reviews, versions, orchestrations);

        var result = Assert.IsType<JsonResult>(await page.OnGetListAsync());

        Assert.Contains("Orchestration unavailable", System.Text.Json.JsonSerializer.Serialize(result.Value));
    }

    [Theory]
    [InlineData(true, 503)]
    [InlineData(false, 500)]
    public async Task ListReturnsFriendlyErrors(bool unsupported, int expectedStatus)
    {
        var reviews = Substitute.For<IOrchestrationVersionReviewApplicationService>();
        reviews.GetAll(Arg.Any<GetAllOrchestrationVersionReviewsQuery>(), Arg.Any<CancellationToken>())
            .Returns<Task<ApplicationPagedResult<OrchestrationVersionReviewModel>>>(_ => throw (unsupported ? (Exception)new NotSupportedException() : new InvalidOperationException()));
        var page = CreatePage(reviews, Substitute.For<IOrchestrationVersionApplicationService>(), Substitute.For<IOrchestrationApplicationService>());

        Assert.Equal(expectedStatus, Assert.IsType<ObjectResult>(await page.OnGetListAsync()).StatusCode);
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

    private static ReviewsIndexModel CreatePage(IOrchestrationVersionReviewApplicationService reviews,
        IOrchestrationVersionApplicationService versions, IOrchestrationApplicationService orchestrations)
    {
        var url = Substitute.For<IUrlHelper>();
        url.ActionContext.Returns(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()));
        url.RouteUrl(Arg.Any<UrlRouteContext>()).Returns("/reviews/review-1");
        return new ReviewsIndexModel(reviews, versions, orchestrations) { Url = url };
    }

    private static OrchestrationVersionReviewModel CreateReview() => new()
    {
        Id = "review-1", OrchestrationVersionId = "version-1", Operation = OrchestrationVersionReviewOperation.Deploy,
        ApprovalStatus = OrchestrationVersionReviewApprovalStatus.Pending, ApplicationStatus = OrchestrationVersionReviewApplicationStatus.Pending,
        RequestedBy = "designer", RequestedOnUtc = DateTime.UtcNow
    };
}
