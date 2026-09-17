using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using ArtifactsIndexModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Artifacts.IndexModel;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ControlPlaneArtifactsPageModelTests
{
    [Fact]
    public async Task OnGetWithoutSelectedOrchestrationLoadsOrchestrationsButNoRows()
    {
        var fixture = new Fixture();
        var page = fixture.CreatePage();

        await page.OnGetAsync(CancellationToken.None);

        Assert.Single(page.Orchestrations);
        Assert.Empty(page.Rows);
        Assert.Null(page.SelectedOrchestration);
    }

    [Fact]
    public async Task OnGetFiltersArtifactsBySelectedOrchestrationAndSortsNewestFirst()
    {
        var fixture = new Fixture();
        var page = fixture.CreatePage();
        page.OrchestrationId = "sales.sale.created";

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal("Sales Sale Created", page.SelectedOrchestration.Name);
        Assert.Equal(["artifact-new", "artifact-old"], page.Rows.Select(x => x.Id).ToArray());
        Assert.All(page.Rows, artifact => Assert.Equal("sales.sale.created", artifact.OrchestrationDefinitionId));
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
