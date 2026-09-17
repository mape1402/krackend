using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using ReleasesIndexModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Releases.IndexModel;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ControlPlaneReleasesPageModelTests
{
    [Fact]
    public async Task OnGetLoadsRowsArtifactsRuntimeNodesTargetsAndPoliciesForSelectedOrchestration()
    {
        var fixture = new Fixture();
        fixture.OrchestrationId = "sales.sale.created";
        var page = fixture.CreatePage();
        page.OrchestrationId = fixture.OrchestrationId;

        await page.OnGetAsync(CancellationToken.None);

        Assert.Single(page.Rows);
        Assert.Equal("release-sales", page.Rows.Single().Id);
        Assert.Single(page.SelectedArtifacts);
        Assert.Equal("artifact-sales", page.SelectedArtifacts.Single().Id);
        Assert.Single(page.RuntimeNodes);
        Assert.Equal("runtime-enabled", page.RuntimeNodes.Single().Id);
        Assert.Equal("Sales Sale Created", page.SelectedOrchestration.Name);
        Assert.True(page.ReleaseTargetByReleaseAndNode.ContainsKey("release-sales|runtime-enabled"));
        Assert.Equal(["runtime-enabled"], page.AllowedNodeIdsByOrchestrationId["sales.sale.created"]);
    }

    [Fact]
    public async Task OnPostCreateRedirectsWhenNoOrchestrationIsSelected()
    {
        var fixture = new Fixture();
        var page = fixture.CreatePage();
        page.Input = new ReleasesIndexModel.ReleaseInput();

        var result = await page.OnPostCreateAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        await fixture.ReleaseService.DidNotReceive().Create(Arg.Any<CreateReleaseInput>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnPostCreateReturnsPageWhenArtifactDoesNotBelongToSelectedOrchestration()
    {
        var fixture = new Fixture();
        var page = fixture.CreatePage();
        page.Input = new ReleasesIndexModel.ReleaseInput
        {
            OrchestrationDefinitionId = "other.orchestration",
            ArtifactId = "artifact-sales",
            RuntimeNodeIds = ["runtime-enabled"]
        };

        var result = await page.OnPostCreateAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(page.ModelState.IsValid);
        Assert.Contains(page.ModelState[string.Empty]!.Errors, error => error.ErrorMessage.Contains("Selected artifact", StringComparison.Ordinal));
        await fixture.ReleaseService.DidNotReceive().Create(Arg.Any<CreateReleaseInput>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnPostCreateRequiresAtLeastOneRuntimeNode()
    {
        var fixture = new Fixture();
        var page = fixture.CreatePage();
        page.Input = new ReleasesIndexModel.ReleaseInput
        {
            OrchestrationDefinitionId = "sales.sale.created",
            ArtifactId = "artifact-sales",
            RuntimeNodeIds = []
        };

        var result = await page.OnPostCreateAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(page.ModelState.IsValid);
        Assert.Contains(nameof(page.Input.RuntimeNodeIds), page.ModelState.Keys);
    }

    [Fact]
    public async Task OnPostCreateCreatesReleaseWithDistinctRuntimeNodes()
    {
        var fixture = new Fixture();
        CreateReleaseInput? captured = null;
        fixture.ReleaseService
            .Create(Arg.Do<CreateReleaseInput>(input => captured = input), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("release-new"));
        var page = fixture.CreatePage();
        page.Input = new ReleasesIndexModel.ReleaseInput
        {
            OrchestrationDefinitionId = "sales.sale.created",
            ArtifactId = "artifact-sales",
            RequestedBy = "tester",
            Strategy = "Immediate",
            RuntimeNodeIds = ["runtime-enabled", "", "runtime-enabled"],
            Notes = "rollout"
        };

        var result = await page.OnPostCreateAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("sales.sale.created", redirect.RouteValues!["OrchestrationId"]);
        Assert.NotNull(captured);
        Assert.Equal("artifact-sales", captured!.ArtifactId);
        Assert.Equal(["runtime-enabled"], captured.RuntimeNodeIds);
        Assert.Equal("rollout", captured.Notes);
    }

    [Fact]
    public async Task OnPostCreateSurfacesReleaseServiceErrors()
    {
        var fixture = new Fixture();
        fixture.ReleaseService
            .Create(Arg.Any<CreateReleaseInput>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new InvalidOperationException("node policy missing"));
        var page = fixture.CreatePage();
        page.Input = new ReleasesIndexModel.ReleaseInput
        {
            OrchestrationDefinitionId = "sales.sale.created",
            ArtifactId = "artifact-sales",
            RuntimeNodeIds = ["runtime-enabled"]
        };

        var result = await page.OnPostCreateAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Contains(page.ModelState[string.Empty]!.Errors, error => error.ErrorMessage == "node policy missing");
    }

    [Fact]
    public async Task PushSetsMessageForSuccessFailureAndExceptions()
    {
        var fixture = new Fixture();
        fixture.DeliveryService.Push("target-success", "web-ui", Arg.Any<CancellationToken>())
            .Returns(new RuntimeArtifactDeliveryResult { Succeeded = true, Message = "ready" });
        fixture.DeliveryService.Push("target-failed", "web-ui", Arg.Any<CancellationToken>())
            .Returns(new RuntimeArtifactDeliveryResult { Succeeded = false, Message = "not ready" });
        fixture.DeliveryService.Push("target-exception", "web-ui", Arg.Any<CancellationToken>())
            .Returns<RuntimeArtifactDeliveryResult>(_ => throw new InvalidOperationException("runtime unreachable"));
        var page = fixture.CreatePage();

        Assert.IsType<RedirectToPageResult>(await page.OnPostPushAsync("target-success", "sales.sale.created", CancellationToken.None));
        Assert.Equal("Artifact promoted", page.TempData["OrchestratorMessage.Title"]);
        Assert.Equal("success", page.TempData["OrchestratorMessage.Type"]);

        Assert.IsType<RedirectToPageResult>(await page.OnPostPushAsync("target-failed", "sales.sale.created", CancellationToken.None));
        Assert.Equal("Artifact promotion failed", page.TempData["OrchestratorMessage.Title"]);
        Assert.Equal("error", page.TempData["OrchestratorMessage.Type"]);

        Assert.IsType<RedirectToPageResult>(await page.OnPostPushAsync("target-exception", "sales.sale.created", CancellationToken.None));
        Assert.Equal("Artifact promotion failed", page.TempData["OrchestratorMessage.Title"]);
        Assert.Equal("runtime unreachable", page.TempData["OrchestratorMessage.Body"]);
    }

    [Fact]
    public async Task AllowedNodeEndpointsReturnAndSavePolicy()
    {
        var fixture = new Fixture();
        ReplaceOrchestrationNodePolicyInput? captured = null;
        fixture.PolicyService
            .Replace(Arg.Do<ReplaceOrchestrationNodePolicyInput>(input => captured = input), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var page = fixture.CreatePage();

        var emptyResult = await page.OnGetAllowedNodesAsync(string.Empty, CancellationToken.None);
        var policyResult = await page.OnGetAllowedNodesAsync("sales.sale.created", CancellationToken.None);
        page.AllowedNodes = new ReleasesIndexModel.AllowedNodesInput
        {
            OrchestrationDefinitionId = "sales.sale.created",
            RuntimeNodeIds = ["runtime-enabled", "", "runtime-enabled"],
            UpdatedBy = ""
        };
        var saveResult = await page.OnPostSaveAllowedNodesAsync(CancellationToken.None);

        AssertJsonArrayLength(emptyResult, "runtimeNodeIds", 0);
        AssertJsonArrayLength(policyResult, "runtimeNodeIds", 1);
        var redirect = Assert.IsType<RedirectToPageResult>(saveResult);
        Assert.Equal("sales.sale.created", redirect.RouteValues!["OrchestrationId"]);
        Assert.NotNull(captured);
        Assert.Equal(["runtime-enabled"], captured!.RuntimeNodeIds);
        Assert.Equal("web-ui", captured.UpdatedBy);
    }

    [Fact]
    public async Task SavingAllowedNodesRequiresSelectedOrchestration()
    {
        var fixture = new Fixture();
        var page = fixture.CreatePage();
        page.AllowedNodes = new ReleasesIndexModel.AllowedNodesInput();

        var result = await page.OnPostSaveAllowedNodesAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(page.ModelState.IsValid);
        await fixture.PolicyService.DidNotReceive().Replace(Arg.Any<ReplaceOrchestrationNodePolicyInput>(), Arg.Any<CancellationToken>());
    }

    private static void AssertJsonArrayLength(IActionResult result, string propertyName, int expectedLength)
    {
        var json = Assert.IsType<JsonResult>(result);
        var document = System.Text.Json.JsonSerializer.SerializeToDocument(json.Value);
        Assert.Equal(expectedLength, document.RootElement.GetProperty(propertyName).GetArrayLength());
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
        public string OrchestrationId { get; set; } = "sales.sale.created";

        public IReleaseApplicationService ReleaseService { get; } = Substitute.For<IReleaseApplicationService>();

        public IReleaseTargetApplicationService ReleaseTargetService { get; } = Substitute.For<IReleaseTargetApplicationService>();

        public IArtifactApplicationService ArtifactService { get; } = Substitute.For<IArtifactApplicationService>();

        public IRuntimeNodeApplicationService RuntimeNodeService { get; } = Substitute.For<IRuntimeNodeApplicationService>();

        public IOrchestrationNodePolicyApplicationService PolicyService { get; } = Substitute.For<IOrchestrationNodePolicyApplicationService>();

        public IArtifactDeliveryApplicationService DeliveryService { get; } = Substitute.For<IArtifactDeliveryApplicationService>();

        public Fixture()
        {
            ReleaseService.GetAll(Arg.Any<ApplicationPagedSettings>(), Arg.Any<CancellationToken>())
                .Returns(Page(
                    new ReleaseModel
                    {
                        Id = "release-sales",
                        ArtifactId = "artifact-sales",
                        OrchestrationDefinitionId = "sales.sale.created",
                        RequestedBy = "tester",
                        Strategy = "Immediate",
                        Status = "Queued",
                        CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2)
                    },
                    new ReleaseModel
                    {
                        Id = "release-other",
                        ArtifactId = "artifact-other",
                        OrchestrationDefinitionId = "other.orchestration",
                        RequestedBy = "tester",
                        Strategy = "Immediate",
                        Status = "Queued",
                        CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
                    }));
            ReleaseService.Create(Arg.Any<CreateReleaseInput>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult("release-created"));
            ArtifactService.GetAll(Arg.Any<ApplicationPagedSettings>(), Arg.Any<CancellationToken>())
                .Returns(Page(
                    Artifact("artifact-sales", "sales.sale.created", DateTime.UtcNow),
                    Artifact("artifact-other", "other.orchestration", DateTime.UtcNow.AddMinutes(1))));
            RuntimeNodeService.GetAll(Arg.Any<ApplicationPagedSettings>(), Arg.Any<CancellationToken>())
                .Returns(Page(
                    RuntimeNode("runtime-enabled", RuntimeNodeStatus.Enabled, false),
                    RuntimeNode("runtime-suspended", RuntimeNodeStatus.Suspend, false),
                    RuntimeNode("runtime-deleted", RuntimeNodeStatus.Enabled, true)));
            ReleaseTargetService.GetAll(Arg.Any<ApplicationPagedSettings>(), Arg.Any<CancellationToken>())
                .Returns(Page(
                    new ReleaseTargetModel
                    {
                        Id = "target-old",
                        ReleaseId = "release-sales",
                        RuntimeNodeId = "runtime-enabled",
                        ArtifactId = "artifact-sales",
                        Status = "Delivered",
                        AssignedAtUtc = DateTime.UtcNow.AddMinutes(-5)
                    },
                    new ReleaseTargetModel
                    {
                        Id = "target-new",
                        ReleaseId = "release-sales",
                        RuntimeNodeId = "runtime-enabled",
                        ArtifactId = "artifact-sales",
                        Status = "Pending",
                        AssignedAtUtc = DateTime.UtcNow
                    },
                    new ReleaseTargetModel
                    {
                        Id = "target-other",
                        ReleaseId = "release-other",
                        RuntimeNodeId = "runtime-enabled",
                        ArtifactId = "artifact-other",
                        Status = "Pending",
                        AssignedAtUtc = DateTime.UtcNow
                    }));
            PolicyService.GetOrchestrations(Arg.Any<CancellationToken>())
                .Returns([
                    new OrchestrationPolicyDefinitionModel
                    {
                        Id = "sales.sale.created",
                        Key = "sales.sale.created",
                        Name = "Sales Sale Created",
                        IsActive = true
                    },
                    new OrchestrationPolicyDefinitionModel
                    {
                        Id = "other.orchestration",
                        Key = "other.orchestration",
                        Name = "Other",
                        IsActive = true
                    }
                ]);
            PolicyService.Get("sales.sale.created", Arg.Any<CancellationToken>())
                .Returns(new OrchestrationNodePolicyModel
                {
                    OrchestrationDefinitionId = "sales.sale.created",
                    RuntimeNodeIds = ["runtime-enabled"]
                });
            PolicyService.GetByOrchestrationIds(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(new Dictionary<string, IReadOnlyCollection<string>>
                {
                    ["sales.sale.created"] = ["runtime-enabled"],
                    ["other.orchestration"] = []
                });
        }

        public ReleasesIndexModel CreatePage()
        {
            var page = new ReleasesIndexModel(
                ReleaseService,
                ReleaseTargetService,
                ArtifactService,
                RuntimeNodeService,
                PolicyService,
                DeliveryService);
            var httpContext = new DefaultHttpContext();
            page.PageContext = new PageContext { HttpContext = httpContext };
            page.TempData = new TempDataDictionary(httpContext, new InMemoryTempDataProvider());
            return page;
        }

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

        private static RuntimeNodeModel RuntimeNode(string id, RuntimeNodeStatus status, bool isDeleted)
            => new()
            {
                Id = id,
                Name = id,
                Code = id,
                DistributionMode = DistributionMode.HybridSync.ToString(),
                EndpointBaseUri = "https://runtime.local",
                Status = status.ToString(),
                IsEnabled = status == RuntimeNodeStatus.Enabled,
                IsDeleted = isDeleted,
                RegisteredAtUtc = DateTime.UtcNow
            };
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        private IDictionary<string, object> _values = new Dictionary<string, object>(StringComparer.Ordinal);

        public IDictionary<string, object> LoadTempData(HttpContext context)
            => _values;

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
            _values = new Dictionary<string, object>(values, StringComparer.Ordinal);
        }
    }
}
