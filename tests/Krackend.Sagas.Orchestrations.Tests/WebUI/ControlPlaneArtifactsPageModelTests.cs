using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Microsoft.AspNetCore.Mvc;
using ArtifactsIndexModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Artifacts.IndexModel;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ControlPlaneArtifactsPageModelTests
{
    [Theory]
    [InlineData("orchestration-version.new", "New version", "success")]
    [InlineData("orchestration-version.deprecated", "Deprecated", "warning")]
    [InlineData("orchestration-version.archived", "Archived", "danger")]
    [InlineData("orchestration-version-snapshot", "Snapshot", "")]
    [InlineData("custom-artifact", "custom-artifact", "")]
    public void ArtifactTypePresentationUsesSharedZoorceLifecycleTones(
        string artifactType,
        string expectedLabel,
        string expectedTone)
    {
        var fixture = new Fixture();
        var presentation = fixture.CreatePage().GetArtifactTypePresentation(artifactType);

        Assert.Equal(expectedLabel, presentation.Label);
        Assert.Equal(expectedTone, presentation.Tone);
    }

    [Fact]
    public async Task OnGetDoesNotLoadTheEntireCatalog()
    {
        var fixture = new Fixture();
        var page = fixture.CreatePage();

        await page.OnGetAsync(CancellationToken.None);

        await fixture.Artifacts.DidNotReceiveWithAnyArgs().GetAll(default!, default);
    }

    [Fact]
    public async Task ListPassesFilteringAndPagingToTheStorageService()
    {
        var fixture = new Fixture();
        var page = fixture.CreatePage();
        page.OrchestrationId = "sales.sale.created";

        var result = await page.OnGetListAsync(2, 25, "sale", "created", true);

        Assert.IsType<JsonResult>(result);
        await fixture.Artifacts.Received(1).Search(Arg.Is<ApplicationPagedSettings>(x => x.PageNumber == 2 && x.PageSize == 25),
            "sales.sale.created", "sale", "created", true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DetailsLoadsFullArtifactOnlyWhenRequested()
    {
        var fixture = new Fixture();
        var id = Ulid.NewUlid().ToString();
        fixture.Artifacts.GetById(id, Arg.Any<CancellationToken>()).Returns(new ArtifactModel
        {
            Id = id, OrchestrationDisplayName = "Sales", Payload = "{\"sample\":true}"
        });
        var page = fixture.CreatePage();

        var result = await page.OnGetDetailsAsync(id);

        Assert.IsType<JsonResult>(result);
        await fixture.Artifacts.Received(1).GetById(id, Arg.Any<CancellationToken>());
        Assert.IsType<BadRequestObjectResult>(await page.OnGetDetailsAsync("invalid"));
    }

    private static ApplicationPagedResult<T> Page<T>(params T[] rows)
        => new()
        {
            PageNumber = 1,
            PageSize = 100,
            TotalRows = rows.Length,
            TotalPages = 1,
            Rows = rows
        };

    private sealed class Fixture
    {
        private readonly IArtifactApplicationService _artifactService = Substitute.For<IArtifactApplicationService>();
        private readonly IOrchestrationNodePolicyApplicationService _policyService = Substitute.For<IOrchestrationNodePolicyApplicationService>();
        public IArtifactApplicationService Artifacts => _artifactService;

        public Fixture()
        {
            _policyService.GetOrchestrations(Arg.Any<CancellationToken>())
                .Returns([
                    new OrchestrationPolicyDefinitionModel
                    {
                        Id = "sales.sale.created",
                        Key = "sales.sale.created",
                        Name = "Sales Sale Created",
                        IsActive = true
                    }
                ]);
            _artifactService.GetAll(Arg.Any<ApplicationPagedSettings>(), Arg.Any<CancellationToken>())
                .Returns(Page(
                    Artifact("artifact-old", "sales.sale.created", DateTime.UtcNow.AddMinutes(-10)),
                    Artifact("artifact-other", "other.orchestration", DateTime.UtcNow),
                    Artifact("artifact-new", "sales.sale.created", DateTime.UtcNow.AddMinutes(10))));
            _artifactService.Search(Arg.Any<ApplicationPagedSettings>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(Page(Artifact("artifact-new", "sales.sale.created", DateTime.UtcNow)));
        }

        public ArtifactsIndexModel CreatePage()
            => new(_artifactService, _policyService);

        private static ArtifactModel Artifact(string id, string orchestrationDefinitionId, DateTime createdAtUtc)
            => new()
            {
                Id = id,
                OrchestrationDefinitionId = orchestrationDefinitionId,
                OrchestrationVersionId = $"{id}-version",
                OrchestrationDisplayName = orchestrationDefinitionId,
                VersionLabel = "1.0.0",
                VersionNumber = "1.0.0",
                ArtifactType = "orchestration-version-snapshot",
                SchemaVersion = "1.0",
                Payload = "{}",
                Metadata = "{}",
                SourceEvent = "tests",
                SourceVersion = "1.0.0",
                Checksum = $"checksum-{id}",
                CreatedAtUtc = createdAtUtc
            };
    }
}
