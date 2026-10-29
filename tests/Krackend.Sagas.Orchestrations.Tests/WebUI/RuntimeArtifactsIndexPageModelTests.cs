using System.Text.Json.Nodes;
using System.Text;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Artifacts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class RuntimeArtifactsIndexPageModelTests
{
    [Fact]
    public async Task OnGetAsync_DoesNotLoadAllArtifacts()
    {
        var repository = new ArtifactRepositoryStub(
            Artifact("sales.sale.created", new SemanticVersion(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready, DateTime.UtcNow.AddHours(-2), checksum: "checksum-old"),
            Artifact("sales.sale.created", new SemanticVersion(1, 1, 0), RuntimeOrchestrationArtifactStatus.Pending, DateTime.UtcNow, checksum: "checksum-new"),
            Artifact("payments.sale.created", new SemanticVersion(2, 0, 0), RuntimeOrchestrationArtifactStatus.Failed, DateTime.UtcNow.AddHours(-1), checksum: "payments-checksum", error: "projection failed"));
        var model = CreateModel(repository);
        await model.OnGetAsync();
        Assert.Equal(0, repository.GetAllCalls);
    }

    [Fact]
    public async Task CatalogEndpointsReturnUnavailableForStorageWithoutCatalogSupport()
    {
        var artifact = Artifact("sales.sale.created", new SemanticVersion(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready, DateTime.UtcNow);
        var history = new DeploymentHistoryRepositoryStub(
        [
            Record(artifact, "orchestration-version.new", RuntimeOrchestrationLifecycleStatus.Active),
            Record(artifact, "orchestration-version.deprecated", RuntimeOrchestrationLifecycleStatus.Deprecated)
        ]);
        var model = CreateModel(new ArtifactRepositoryStub(artifact), history: history);

        Assert.Equal(503, Assert.IsType<StatusCodeResult>(await model.OnGetGroupsAsync()).StatusCode);
        Assert.Equal(503, Assert.IsType<StatusCodeResult>(await model.OnGetVersionsAsync("sales")).StatusCode);
        Assert.Equal(503, Assert.IsType<StatusCodeResult>(await model.OnGetHistoryAsync(artifact.Id.ToString())).StatusCode);
    }

    [Fact]
    public async Task CatalogEndpointsRejectMissingKeysAndInvalidArtifactIds()
    {
        var artifact = Artifact("sales.sale.created", new SemanticVersion(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready, DateTime.UtcNow);
        var model = CreateModel(new ArtifactRepositoryStub(artifact));
        Assert.IsType<BadRequestResult>(await model.OnGetVersionsAsync(""));
        Assert.IsType<BadRequestResult>(await model.OnGetHistoryAsync("invalid"));
    }

    [Fact]
    public async Task OnPostRequestStandupAsync_WhenArtifactIsReadyAndActive_NotifiesReplicas()
    {
        var artifact = Artifact("sales.sale.created", new SemanticVersion(1, 2, 0), RuntimeOrchestrationArtifactStatus.Ready, DateTime.UtcNow);
        var repository = new ArtifactRepositoryStub(artifact);
        var notifier = new RecordingRuntimeArtifactReadyNotifier();
        var model = CreateModel(repository, notifier);

        var result = await model.OnPostRequestStandupAsync(artifact.Id.ToString());

        Assert.IsType<RedirectToPageResult>(result);
        var message = Assert.Single(notifier.Messages);
        Assert.Equal(artifact.Id.ToString(), message.ArtifactId);
        Assert.Equal("sales.sale.created", message.OrchestrationDefinitionKey);
        Assert.Equal("1.2.0", message.Version);
        Assert.Equal("Standup requested", model.TempData["OrchestratorMessage.Title"]);
        Assert.Equal("success", model.TempData["OrchestratorMessage.Type"]);
    }

    [Fact]
    public async Task OnPostRequestStandupAsync_WhenArtifactIdIsInvalid_ReturnsWarningMessageWithoutRepositoryLookup()
    {
        var repository = new ArtifactRepositoryStub();
        var notifier = new RecordingRuntimeArtifactReadyNotifier();
        var model = CreateModel(repository, notifier);

        var result = await model.OnPostRequestStandupAsync("not-an-ulid");

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(0, repository.GetByIdCalls);
        Assert.Empty(notifier.Messages);
        Assert.Equal("Invalid artifact", model.TempData["OrchestratorMessage.Title"]);
        Assert.Equal("error", model.TempData["OrchestratorMessage.Type"]);
    }

    [Fact]
    public async Task OnPostRequestStandupAsync_WhenArtifactIsNotReady_DoesNotNotifyReplicas()
    {
        var artifact = Artifact("sales.sale.created", new SemanticVersion(1, 3, 0), RuntimeOrchestrationArtifactStatus.Pending, DateTime.UtcNow);
        var repository = new ArtifactRepositoryStub(artifact);
        var notifier = new RecordingRuntimeArtifactReadyNotifier();
        var model = CreateModel(repository, notifier);

        var result = await model.OnPostRequestStandupAsync(artifact.Id.ToString());

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Empty(notifier.Messages);
        Assert.Equal("Artifact is not ready", model.TempData["OrchestratorMessage.Title"]);
        Assert.Equal("warning", model.TempData["OrchestratorMessage.Type"]);
    }

    [Fact]
    public async Task OnPostRequestStandupAsync_WhenArtifactIsArchived_DoesNotNotifyReplicas()
    {
        var artifact = Artifact("sales.sale.created", new SemanticVersion(1, 4, 0), RuntimeOrchestrationArtifactStatus.Retired, DateTime.UtcNow);
        artifact.IsActive = false;
        artifact.LifecycleStatus = RuntimeOrchestrationLifecycleStatus.Archived;
        var repository = new ArtifactRepositoryStub(artifact);
        var notifier = new RecordingRuntimeArtifactReadyNotifier();
        var model = CreateModel(repository, notifier);

        var result = await model.OnPostRequestStandupAsync(artifact.Id.ToString());

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Empty(notifier.Messages);
        Assert.Equal("Artifact is not ready", model.TempData["OrchestratorMessage.Title"]);
        Assert.Equal("warning", model.TempData["OrchestratorMessage.Type"]);
    }

    [Fact]
    public async Task OnPostRequestStandupAsync_WhenRepositoryFails_ShowsFailureMessage()
    {
        var artifactId = Id.New();
        var repository = new ArtifactRepositoryStub();
        repository.ThrowOnGetById = true;
        var notifier = new RecordingRuntimeArtifactReadyNotifier();
        var model = CreateModel(repository, notifier);

        var result = await model.OnPostRequestStandupAsync(artifactId.ToString());

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Empty(notifier.Messages);
        Assert.Equal("Standup request failed", model.TempData["OrchestratorMessage.Title"]);
        Assert.Equal("Artifact lookup failed.", model.TempData["OrchestratorMessage.Body"]);
        Assert.Equal("error", model.TempData["OrchestratorMessage.Type"]);
    }

    [Fact]
    public async Task OnGetExportCandidateAsync_DownloadsThePortableRuntimeCandidate()
    {
        var artifact = Artifact("sales.sale.created", new SemanticVersion(1, 5, 0), RuntimeOrchestrationArtifactStatus.Ready, DateTime.UtcNow);
        var transfer = Substitute.For<IRuntimeArtifactTransferService>();
        var package = new RuntimeArtifactTransferPackage
        {
            SourceArtifactId = artifact.Id.ToString(),
            PackageChecksum = "sha256:package",
            Delivery = new RuntimeArtifactDeliveryPackage { OrchestrationDefinitionKey = "sales.sale.created", Version = "1.5.0" }
        };
        transfer.ExportAsync(artifact.Id.ToString(), Arg.Any<CancellationToken>()).Returns(package);
        transfer.Serialize(package).Returns("{\"package\":true}");
        var model = CreateModel(new ArtifactRepositoryStub(artifact), transfer: transfer);

        var result = await model.OnGetExportCandidateAsync(artifact.Id.ToString());

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/json", file.ContentType);
        Assert.Contains("sales.sale.created-1.5.0", file.FileDownloadName, StringComparison.Ordinal);
        Assert.Equal("{\"package\":true}", Encoding.UTF8.GetString(file.FileContents));
    }

    [Fact]
    public async Task OnPostImportCandidateAsync_UsesTheTransferServiceAndReportsItsDeploymentOutcome()
    {
        var transfer = Substitute.For<IRuntimeArtifactTransferService>();
        transfer.ImportAsync("{\"package\":true}", "runtime-ui", Arg.Any<CancellationToken>())
            .Returns(new RuntimeArtifactDeploymentResult { Accepted = true, Status = "Pending", Message = "Accepted for projection." });
        var model = CreateModel(new ArtifactRepositoryStub(), transfer: transfer);
        var file = new FormFile(new MemoryStream(Encoding.UTF8.GetBytes("{\"package\":true}")), 0, 16, "candidatePackage", "candidate.json");

        var result = await model.OnPostImportCandidateAsync(file);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Artifact candidate applied", model.TempData["OrchestratorMessage.Title"]);
        Assert.Equal("success", model.TempData["OrchestratorMessage.Type"]);
    }

    [Fact]
    public async Task OnPostImportCandidateAsync_RejectsMissingFilesBeforeCallingTheTransferService()
    {
        var transfer = Substitute.For<IRuntimeArtifactTransferService>();
        var model = CreateModel(new ArtifactRepositoryStub(), transfer: transfer);

        var result = await model.OnPostImportCandidateAsync(null!);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Artifact import failed", model.TempData["OrchestratorMessage.Title"]);
        Assert.Equal("warning", model.TempData["OrchestratorMessage.Type"]);
        await transfer.DidNotReceive().ImportAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnPostInspectCandidateAsync_ReturnsPortableCandidateDetailsWithoutApplyingIt()
    {
        var transfer = Substitute.For<IRuntimeArtifactTransferService>();
        transfer.Inspect("{\"package\":true}").Returns(new RuntimeArtifactTransferInspection(
            1, RuntimeArtifactTransferPackage.Kind, "source-artifact", "sales.sale.created", "1.5.0",
            "orchestration-version.new", "Rollout", DateTime.UtcNow));
        var model = CreateModel(new ArtifactRepositoryStub(), transfer: transfer);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"package\":true}"));
        var file = new FormFile(stream, 0, stream.Length, "candidatePackage", "candidate.json");

        var result = await model.OnPostInspectCandidateAsync(file);

        var payload = Assert.IsType<JsonResult>(result).Value;
        Assert.Contains("sales.sale.created", System.Text.Json.JsonSerializer.Serialize(payload), StringComparison.Ordinal);
        await transfer.DidNotReceive().ImportAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static IndexModel CreateModel(
        IRuntimeArtifactRepository artifactRepository,
        IRuntimeArtifactReadyNotifier? notifier = null,
        IRuntimeArtifactDeploymentHistoryRepository? history = null,
        IRuntimeArtifactTransferService? transfer = null)
    {
        var model = new IndexModel(
            artifactRepository,
            history ?? new DeploymentHistoryRepositoryStub(),
            notifier ?? new RecordingRuntimeArtifactReadyNotifier(),
            transfer ?? Substitute.For<IRuntimeArtifactTransferService>());
        model.TempData = new TempDataDictionary(new DefaultHttpContext(), new EmptyTempDataProvider());
        return model;
    }

    private static RuntimeOrchestrationArtifact Artifact(
        string key,
        SemanticVersion version,
        RuntimeOrchestrationArtifactStatus status,
        DateTime deployedOnUtc,
        string checksum = "checksum",
        string? error = null)
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = key,
            ArtifactType = "orchestration-version-snapshot",
            SourceOrchestrationVersionId = Id.New(),
            Version = version,
            ArtifactChecksum = new Checksum(checksum),
            ArtifactPayload = JsonNode.Parse("""{"definitionKey":"sales.sale.created"}""")!,
            Status = status,
            IsActive = true,
            IngressGeneration = 3,
            DeployedOnUtc = deployedOnUtc,
            ActivatedOnUtc = status == RuntimeOrchestrationArtifactStatus.Ready ? deployedOnUtc.AddMinutes(1) : null,
            ProjectionStartedOnUtc = deployedOnUtc.AddSeconds(10),
            ProjectionCompletedOnUtc = status == RuntimeOrchestrationArtifactStatus.Ready ? deployedOnUtc.AddSeconds(20) : null,
            ProjectionFailedOnUtc = status == RuntimeOrchestrationArtifactStatus.Failed ? deployedOnUtc.AddSeconds(20) : null,
            ProjectionError = error
        };

    private static RuntimeArtifactDeploymentRecord Record(
        RuntimeOrchestrationArtifact artifact,
        string artifactType,
        RuntimeOrchestrationLifecycleStatus lifecycleStatus)
        => new()
        {
            Id = Id.New(),
            RuntimeOrchestrationArtifactId = artifact.Id,
            OrchestrationDefinitionKey = artifact.OrchestrationDefinitionKey,
            OrchestrationVersion = artifact.Version.ToString(),
            ControlPlaneArtifactId = Id.New().ToString(),
            ReleaseId = Id.New().ToString(),
            ArtifactType = artifactType,
            ReleaseStrategy = "Direct",
            LifecycleStatus = lifecycleStatus,
            RuntimeStatus = RuntimeOrchestrationArtifactStatus.Ready,
            AppliedOnUtc = DateTime.UtcNow
        };

    private sealed class ArtifactRepositoryStub(params RuntimeOrchestrationArtifact[] artifacts) : IRuntimeArtifactRepository
    {
        private readonly Dictionary<Id, RuntimeOrchestrationArtifact> _artifacts = artifacts.ToDictionary(x => x.Id);

        public bool ThrowOnGetById { get; set; }

        public int GetByIdCalls { get; private set; }
        public int GetAllCalls { get; private set; }

        public Task Upsert(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task MarkProjectionStarted(Id artifactId, long ingressGeneration, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task MarkReady(Id artifactId, long ingressGeneration, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task MarkProjectionFailed(Id artifactId, long ingressGeneration, string error, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeactivateActiveArtifacts(string orchestrationDefinitionKey, Id exceptArtifactId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RuntimeOrchestrationArtifact> GetById(Id artifactId, CancellationToken cancellationToken = default)
        {
            GetByIdCalls++;
            if (ThrowOnGetById)
            {
                throw new InvalidOperationException("Artifact lookup failed.");
            }

            return Task.FromResult(_artifacts[artifactId]);
        }

        public Task<RuntimeOrchestrationArtifact> GetByVersion(string orchestrationDefinitionKey, SemanticVersion version, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetAll(CancellationToken cancellationToken = default)
        {
            GetAllCalls++;
            return Task.FromResult<IReadOnlyCollection<RuntimeOrchestrationArtifact>>(_artifacts.Values.ToArray());
        }

        public Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetReady(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RuntimeOrchestrationArtifact> GetActive(string orchestrationDefinitionKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingRuntimeArtifactReadyNotifier : IRuntimeArtifactReadyNotifier
    {
        public List<RuntimeArtifactReadyGossipMessage> Messages { get; } = [];

        public Task NotifyReadyAsync(RuntimeArtifactReadyGossipMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class DeploymentHistoryRepositoryStub(params RuntimeArtifactDeploymentRecord[] records) : IRuntimeArtifactDeploymentHistoryRepository
    {
        public Task Append(RuntimeArtifactDeploymentRecord record, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<RuntimeArtifactDeploymentRecord>> GetAll(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<RuntimeArtifactDeploymentRecord>>(records);
    }

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }
}
