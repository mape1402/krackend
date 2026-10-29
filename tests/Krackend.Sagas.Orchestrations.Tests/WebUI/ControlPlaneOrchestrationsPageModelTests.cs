using System.Reflection;
using System.Text;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using CreateOrchestrationPageModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.Orchestrations.CreateModel;
using OrchestrationDetailsPageModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.Orchestrations.DetailsModel;
using OrchestrationsIndexPageModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.Orchestrations.IndexModel;
using DesignPagedSettings = Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.ApplicationPagedSettings;
using DesignPagedResult = Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.ApplicationPagedResult<Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.OrchestrationDefinitionModel>;
using VersionPagedResult = Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.ApplicationPagedResult<Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.OrchestrationVersionModel>;
using DomainPagedResult = Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.ApplicationPagedResult<Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.DomainModel>;
using TeamPagedResult = Krackend.Sagas.Orchestrations.ControlPlane.Application.Security.ApplicationPagedResult<Krackend.Sagas.Orchestrations.ControlPlane.Application.Security.TeamModel>;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ControlPlaneOrchestrationsPageModelTests
{
    [Fact]
    public async Task IndexLoadsOnlyRequestedPageAndForwardsSearchAndSorting()
    {
        var fixture = new Fixture();
        var page = fixture.CreateIndexPage();

        await page.OnGetAsync(0, CancellationToken.None);

        await fixture.OrchestrationService.DidNotReceive().GetAll(Arg.Any<GetOrchestrationDefinitionsQuery>(), Arg.Any<CancellationToken>());
        Assert.IsType<JsonResult>(await page.OnGetListAsync(2, 10, "  sale  ", "key", true, CancellationToken.None));
        await fixture.OrchestrationService.Received(1).GetAll(
            Arg.Is<GetOrchestrationDefinitionsQuery>(query =>
                query.Settings.PageNumber == 2 && query.Settings.PageSize == 10 && query.SearchText == "sale" &&
                query.Settings.Sorts.Single().Field == "key" && query.Settings.Sorts.Single().Descending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexCreatesUpdatesAndTogglesOrchestration()
    {
        var fixture = new Fixture();
        CreateOrchestrationDefinitionCommand? createCommand = null;
        UpdateOrchestrationDefinitionCommand? updateCommand = null;
        fixture.OrchestrationService.Create(
                Arg.Do<CreateOrchestrationDefinitionCommand>(command => createCommand = command),
                Arg.Any<CancellationToken>())
            .Returns("orchestration-created");
        fixture.OrchestrationService.Update(
                Arg.Do<UpdateOrchestrationDefinitionCommand>(command => updateCommand = command),
                Arg.Any<CancellationToken>())
            .Returns(true);
        var page = fixture.CreateIndexPage();
        page.NewOrchestration = new OrchestrationsIndexPageModel.CreateOrchestrationInput
        {
            Key = " sales.sale.created ",
            Name = " Sale Created ",
            DomainId = " domain-1 ",
            Description = " description ",
            OwnerTeamId = " team-1 ",
            Tags = "sales, Sales, fulfillment",
            CreatedBy = " tester "
        };

        var createResult = await page.OnPostUpsertAsync(CancellationToken.None);

        page.NewOrchestration.OrchestrationId = Id.New().ToString();
        page.NewOrchestration.UpdatedBy = " updater ";
        var updateResult = await page.OnPostUpsertAsync(CancellationToken.None);
        var activateResult = await page.OnPostSetActiveAsync("orch-1", CancellationToken.None);
        var deactivateResult = await page.OnPostSetInactiveAsync("orch-1", CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(createResult);
        Assert.IsType<RedirectToPageResult>(updateResult);
        Assert.IsType<RedirectToPageResult>(activateResult);
        Assert.IsType<RedirectToPageResult>(deactivateResult);
        Assert.NotNull(createCommand);
        Assert.Equal("sales.sale.created", createCommand!.Key);
        Assert.Equal(["sales", "fulfillment"], createCommand.Tags.ToArray());
        Assert.NotNull(updateCommand);
        Assert.Equal("updater", updateCommand!.UpdatedBy);
        await fixture.OrchestrationService.Received(1).Activate(Arg.Any<ActivateOrchestrationDefinitionCommand>(), Arg.Any<CancellationToken>());
        await fixture.OrchestrationService.Received(1).Deactivate(Arg.Any<DeactivateOrchestrationDefinitionCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexSuggestionsReturnOnlyActiveRowsSorted()
    {
        var fixture = new Fixture();
        var page = fixture.CreateIndexPage();

        var domainResult = await page.OnGetDomainSuggestionsAsync("sa", CancellationToken.None);
        var teamResult = await page.OnGetTeamSuggestionsAsync("te", CancellationToken.None);

        var domainJson = System.Text.Json.JsonSerializer.SerializeToDocument(Assert.IsType<JsonResult>(domainResult).Value);
        var teamJson = System.Text.Json.JsonSerializer.SerializeToDocument(Assert.IsType<JsonResult>(teamResult).Value);
        Assert.Single(domainJson.RootElement.EnumerateArray());
        Assert.Equal("sales", domainJson.RootElement[0].GetProperty("Key").GetString());
        Assert.Single(teamJson.RootElement.EnumerateArray());
        Assert.Equal("team-a", teamJson.RootElement[0].GetProperty("Key").GetString());
        await fixture.DomainService.Received().GetAll(Arg.Is<GetDomainsQuery>(q => q.ActiveOnly && q.SearchText == "sa" && q.PagedSettings.Sorts.First().Field == "displayName"), Arg.Any<CancellationToken>());
        await fixture.TeamService.Received().GetAll(Arg.Is<GetTeamsQuery>(q => q.ActiveOnly && q.SearchText == "te"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePageCreatesOrchestrationAndSurfacesErrors()
    {
        var fixture = new Fixture();
        CreateOrchestrationDefinitionCommand? captured = null;
        fixture.OrchestrationService.Create(
                Arg.Do<CreateOrchestrationDefinitionCommand>(command => captured = command),
                Arg.Any<CancellationToken>())
            .Returns("orch-created");
        var page = fixture.CreateCreatePage();
        page.Input = new CreateOrchestrationPageModel.CreateOrchestrationInput
        {
            Key = " sales.sale.created ",
            Name = " Sale Created ",
            DomainId = " domain-1 ",
            Description = " description ",
            OwnerTeamId = " team-1 ",
            Tags = "sales,sales,checkout",
            CreatedBy = " tester "
        };

        var result = await page.OnPostAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Orchestrations/Details", redirect.PageName);
        Assert.Equal("orch-created", redirect.RouteValues!["orchestrationId"]);
        Assert.NotNull(captured);
        Assert.Equal("sales.sale.created", captured!.Key);
        Assert.Equal(["sales", "checkout"], captured.Tags.ToArray());

        page.ModelState.AddModelError("Input.Key", "required");
        Assert.IsType<PageResult>(await page.OnPostAsync(CancellationToken.None));

        page.ModelState.Clear();
        fixture.OrchestrationService.Create(Arg.Any<CreateOrchestrationDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new InvalidOperationException("create failed"));
        Assert.IsType<PageResult>(await page.OnPostAsync(CancellationToken.None));
        Assert.Equal("create failed", page.ErrorMessage);
    }

    [Fact]
    public async Task CreatePageSuggestionsReturnActiveRows()
    {
        var fixture = new Fixture();
        var page = fixture.CreateCreatePage();

        var domainResult = await page.OnGetDomainSuggestionsAsync("sa", CancellationToken.None);
        var teamResult = await page.OnGetTeamSuggestionsAsync("te", CancellationToken.None);

        Assert.Single(System.Text.Json.JsonSerializer.SerializeToDocument(Assert.IsType<JsonResult>(domainResult).Value).RootElement.EnumerateArray());
        Assert.Single(System.Text.Json.JsonSerializer.SerializeToDocument(Assert.IsType<JsonResult>(teamResult).Value).RootElement.EnumerateArray());
    }

    [Fact]
    public async Task DetailsLoadsRedirectsNotFoundAndTogglesActiveState()
    {
        var fixture = new Fixture();
        var page = fixture.CreateDetailsPage();

        Assert.IsType<RedirectToPageResult>(await page.OnGetAsync("", CancellationToken.None));
        Assert.IsType<PageResult>(await page.OnGetAsync("orch-1", CancellationToken.None));
        Assert.Equal("orch-1", page.Orchestration.Id);
        Assert.Empty(page.Versions);
        await fixture.VersionService.DidNotReceive().GetAll(Arg.Any<GetOrchestrationVersionsQuery>(), Arg.Any<CancellationToken>());
        Assert.IsType<JsonResult>(await page.OnGetVersionsAsync("orch-1", 2, 10, " sales ", CancellationToken.None));
        await fixture.VersionService.Received(1).GetAll(Arg.Is<GetOrchestrationVersionsQuery>(query =>
            query.Settings.PageNumber == 2 && query.Settings.PageSize == 10 &&
            query.SearchText == "sales" && query.Settings.Sorts.First().Field == "createdOnUtc"), Arg.Any<CancellationToken>());

        fixture.OrchestrationService.GetById(Arg.Any<GetOrchestrationDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<OrchestrationDefinitionModel>(null!));
        Assert.IsType<NotFoundResult>(await page.OnGetAsync("missing", CancellationToken.None));
        Assert.IsType<RedirectToPageResult>(await page.OnPostToggleActiveAsync("missing", CancellationToken.None));

        fixture.OrchestrationService.GetById(Arg.Any<GetOrchestrationDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Orchestration("orch-1", true));
        Assert.IsType<RedirectToPageResult>(await page.OnPostToggleActiveAsync("orch-1", CancellationToken.None));
        await fixture.OrchestrationService.Received().Deactivate(Arg.Any<DeactivateOrchestrationDefinitionCommand>(), Arg.Any<CancellationToken>());

        fixture.OrchestrationService.GetById(Arg.Any<GetOrchestrationDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Orchestration("orch-1", false));
        Assert.IsType<RedirectToPageResult>(await page.OnPostToggleActiveAsync("orch-1", CancellationToken.None));
        await fixture.OrchestrationService.Received().Activate(Arg.Any<ActivateOrchestrationDefinitionCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DetailsCreatesDraftAndRequestsLifecycleReviews()
    {
        var fixture = new Fixture();
        CreateOrchestrationVersionCommand? createCommand = null;
        fixture.VersionService.Create(
                Arg.Do<CreateOrchestrationVersionCommand>(command => createCommand = command),
                Arg.Any<CancellationToken>())
            .Returns("version-created");
        var page = fixture.CreateDetailsPage();
        page.NewVersion = new OrchestrationDetailsPageModel.CreateVersionInput
        {
            CreatedBy = ""
        };

        var createResult = await page.OnPostCreateVersionAsync("orch-1", CancellationToken.None);

        foreach (var action in new[] { "RequestDeployment", "RequestDeprecation", "RequestArchive" })
        {
            var actionResult = Assert.IsType<RedirectToPageResult>(await page.OnPostVersionActionAsync("orch-1", "version-1", action, CancellationToken.None));
            Assert.Equal("/Reviews/Details", actionResult.PageName);
        }
        Assert.IsType<RedirectToPageResult>(await page.OnPostVersionActionAsync("orch-1", "version-1", "Abandon", CancellationToken.None));

        Assert.Equal("/OrchestrationVersions/Details", Assert.IsType<RedirectToPageResult>(createResult).PageName);
        Assert.NotNull(createCommand);
        Assert.Equal("web-ui", createCommand!.CreatedBy);
        Assert.Equal(string.Empty, createCommand.Version);
        await fixture.ReviewService.Received(1).Request(Arg.Is<RequestOrchestrationVersionReviewCommand>(x => x.Operation == OrchestrationVersionReviewOperation.Deploy), Arg.Any<CancellationToken>());
        await fixture.ReviewService.Received(1).Request(Arg.Is<RequestOrchestrationVersionReviewCommand>(x => x.Operation == OrchestrationVersionReviewOperation.Deprecate), Arg.Any<CancellationToken>());
        await fixture.ReviewService.Received(1).Request(Arg.Is<RequestOrchestrationVersionReviewCommand>(x => x.Operation == OrchestrationVersionReviewOperation.Archive), Arg.Any<CancellationToken>());
        await fixture.VersionService.Received(1).Abandon(Arg.Any<AbandonOrchestrationVersionCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DetailsVersionActionSurfacesFriendlyDslErrors()
    {
        var fixture = new Fixture();
        fixture.ReviewService.Request(Arg.Any<RequestOrchestrationVersionReviewCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new OrchestrationArtifactDslValidationException(
                "stage:inventories:task:discountstock:transformation",
                "DSL has semantic errors.")));
        var page = fixture.CreateDetailsPage();

        var result = await page.OnPostVersionActionAsync("orch-1", "version-1", "RequestDeployment", CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Contains("cannot be published", page.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("stage:inventories:task:discountstock:transformation", page.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailsFriendlyErrorMessageHandlesKnownAndUnknownExceptions()
    {
        var method = typeof(OrchestrationDetailsPageModel).GetMethod(
            "ToFriendlyErrorMessage",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        var dsl = (string)method.Invoke(null, [new OrchestrationArtifactDslValidationException("stage:one", "invalid")])!;
        var invalid = (string)method.Invoke(null, [new InvalidOperationException("specific failure")])!;
        var blankInvalid = (string)method.Invoke(null, [new InvalidOperationException("")])!;
        var unknown = (string)method.Invoke(null, [new Exception("hidden")])!;

        Assert.Contains("stage:one", dsl, StringComparison.Ordinal);
        Assert.Equal("specific failure", invalid);
        Assert.Contains("could not be completed", blankInvalid, StringComparison.Ordinal);
        Assert.Contains("could not be completed", unknown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DetailsVersionActionIgnoresCreateVersionValidationState()
    {
        var fixture = new Fixture();
        var page = fixture.CreateDetailsPage();
        page.ModelState.AddModelError("NewVersion.CreatedBy", "required");

        var result = await page.OnPostVersionActionAsync("orch-1", "version-1", "RequestArchive", CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        await fixture.ReviewService.Received(1).Request(Arg.Is<RequestOrchestrationVersionReviewCommand>(x => x.Operation == OrchestrationVersionReviewOperation.Archive), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DetailsHandlesInvalidVersionModelAndExposesAllowedActions()
    {
        var fixture = new Fixture();
        var page = fixture.CreateDetailsPage();
        page.ModelState.AddModelError("NewVersion.CreatedBy", "required");

        var result = await page.OnPostCreateVersionAsync("orch-1", CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(["ExportVersion", "RequestDeployment", "Abandon"], page.GetAllowedActions(OrchestrationVersionStatus.Draft));
        Assert.Equal(["ExportVersion", "OpenReview"], page.GetAllowedActions(OrchestrationVersionStatus.PendingDeployment));
        Assert.Equal(["ExportVersion", "RequestDeprecation"], page.GetAllowedActions(OrchestrationVersionStatus.Deployed));
        Assert.Equal(["ExportVersion", "RequestArchive"], page.GetAllowedActions(OrchestrationVersionStatus.Deprecated));
        Assert.Equal(["ExportVersion"], page.GetAllowedActions(OrchestrationVersionStatus.Archived));
        Assert.Empty(page.GetAllowedActions(OrchestrationVersionStatus.Abandoned));
    }

    [Fact]
    public async Task DetailsImportsPortableVersionAsADraftIntoTheCurrentOrchestration()
    {
        var fixture = new Fixture();
        fixture.PackageService.ImportAsync(
                "orch-1",
                Arg.Any<string>(),
                "web-ui",
                Arg.Any<CancellationToken>())
            .Returns("new-draft");
        var page = fixture.CreateDetailsPage();

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"package\":true}"));
        var file = new FormFile(stream, 0, stream.Length, "importPackage", "sales.krackend.json");

        var result = Assert.IsType<RedirectToPageResult>(await page.OnPostImportDraftAsync("orch-1", file, CancellationToken.None));

        Assert.Equal("/OrchestrationVersions/Details", result.PageName);
        Assert.Equal("orch-1", result.RouteValues!["orchestrationId"]);
        Assert.Equal("new-draft", result.RouteValues["versionId"]);
        await fixture.PackageService.Received(1).ImportAsync(
            "orch-1",
            Arg.Any<string>(),
            "web-ui",
            Arg.Any<CancellationToken>());
    }

    private static DesignPagedResult DefinitionsPage(params OrchestrationDefinitionModel[] rows)
        => new(1, 1, rows.Length, 120, rows);

    private static VersionPagedResult VersionsPage(params OrchestrationVersionModel[] rows)
        => new(1, 1, rows.Length, 200, rows);

    private static DomainPagedResult DomainsPage(params DomainModel[] rows)
        => new(1, 1, rows.Length, 20, rows);

    private static TeamPagedResult TeamsPage(params TeamModel[] rows)
        => new(1, 1, rows.Length, 20, rows);

    private static OrchestrationDefinitionModel Orchestration(string id, bool isActive)
        => new()
        {
            Id = id,
            Key = "sales.sale.created",
            Name = "Sale Created",
            DomainId = "domain-1",
            OwnerTeamId = "team-1",
            Tags = ["sales"],
            IsActive = isActive
        };

    private sealed class Fixture
    {
        public IOrchestrationApplicationService OrchestrationService { get; } = Substitute.For<IOrchestrationApplicationService>();

        public IOrchestrationVersionApplicationService VersionService { get; } = Substitute.For<IOrchestrationVersionApplicationService>();

        public IOrchestrationVersionReviewApplicationService ReviewService { get; } = Substitute.For<IOrchestrationVersionReviewApplicationService>();

        public IOrchestrationVersionPackageService PackageService { get; } = Substitute.For<IOrchestrationVersionPackageService>();

        public IDomainApplicationService DomainService { get; } = Substitute.For<IDomainApplicationService>();

        public ITeamApplicationService TeamService { get; } = Substitute.For<ITeamApplicationService>();

        public Fixture()
        {
            OrchestrationService.GetAll(Arg.Any<GetOrchestrationDefinitionsQuery>(), Arg.Any<CancellationToken>())
                .Returns(DefinitionsPage(Orchestration("orch-1", true)));
            OrchestrationService.GetById(Arg.Any<GetOrchestrationDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
                .Returns(Orchestration("orch-1", true));
            OrchestrationService.Create(Arg.Any<CreateOrchestrationDefinitionCommand>(), Arg.Any<CancellationToken>())
                .Returns("orch-1");
            OrchestrationService.Update(Arg.Any<UpdateOrchestrationDefinitionCommand>(), Arg.Any<CancellationToken>())
                .Returns(true);
            OrchestrationService.Activate(Arg.Any<ActivateOrchestrationDefinitionCommand>(), Arg.Any<CancellationToken>())
                .Returns(true);
            OrchestrationService.Deactivate(Arg.Any<DeactivateOrchestrationDefinitionCommand>(), Arg.Any<CancellationToken>())
                .Returns(true);

            VersionService.GetAll(Arg.Any<GetOrchestrationVersionsQuery>(), Arg.Any<CancellationToken>())
                .Returns(VersionsPage(
                    new OrchestrationVersionModel
                    {
                        Id = "version-old",
                        OrchestrationDefinitionId = "orch-1",
                        Version = "1.0.0",
                        Status = OrchestrationVersionStatus.Draft,
                        CreatedOnUtc = "2026-01-01T00:00:00Z"
                    },
                    new OrchestrationVersionModel
                    {
                        Id = "version-new",
                        OrchestrationDefinitionId = "orch-1",
                        Version = "1.1.0",
                        Status = OrchestrationVersionStatus.Approved,
                        CreatedOnUtc = "2026-01-02T00:00:00Z"
                    }));
            VersionService.GetById(Arg.Any<GetOrchestrationVersionByIdQuery>(), Arg.Any<CancellationToken>())
                .Returns(new OrchestrationVersionModel
                {
                    Id = "version-1",
                    OrchestrationDefinitionId = "orch-1",
                    Status = OrchestrationVersionStatus.Draft,
                });
            VersionService.Create(Arg.Any<CreateOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
                .Returns("version-created");
            VersionService.Update(Arg.Any<UpdateOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
                .Returns(true);
            VersionService.SetInReview(Arg.Any<SetOrchestrationVersionInReviewCommand>(), Arg.Any<CancellationToken>())
                .Returns(true);
            VersionService.ReturnToDraft(Arg.Any<ReturnOrchestrationVersionToDraftCommand>(), Arg.Any<CancellationToken>())
                .Returns(true);
            VersionService.ReopenReview(Arg.Any<ReopenOrchestrationVersionReviewCommand>(), Arg.Any<CancellationToken>())
                .Returns(true);
            VersionService.Approve(Arg.Any<ApproveOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
                .Returns(true);
            VersionService.Deploy(Arg.Any<DeployOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
                .Returns(true);
            VersionService.Deprecate(Arg.Any<DeprecateOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
                .Returns(true);
            VersionService.Archive(Arg.Any<ArchiveOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
                .Returns(true);
            VersionService.Abandon(Arg.Any<AbandonOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
                .Returns(true);
            ReviewService.Request(Arg.Any<RequestOrchestrationVersionReviewCommand>(), Arg.Any<CancellationToken>())
                .Returns("review-1");

            DomainService.GetAll(Arg.Any<GetDomainsQuery>(), Arg.Any<CancellationToken>())
                .Returns(DomainsPage(new DomainModel { Id = "domain-1", Key = "sales", DisplayName = "Sales", Description = "Sales domain", IsActive = true }));
            TeamService.GetAll(Arg.Any<GetTeamsQuery>(), Arg.Any<CancellationToken>())
                .Returns(TeamsPage(new TeamModel { Id = "team-1", Key = "team-a", DisplayName = "Team A", IsActive = true }));
        }

        public OrchestrationsIndexPageModel CreateIndexPage()
            => new(OrchestrationService, DomainService, TeamService);

        public CreateOrchestrationPageModel CreateCreatePage()
            => new(OrchestrationService, DomainService, TeamService);

        public OrchestrationDetailsPageModel CreateDetailsPage()
            => new(OrchestrationService, VersionService, ReviewService, PackageService);
    }
}
