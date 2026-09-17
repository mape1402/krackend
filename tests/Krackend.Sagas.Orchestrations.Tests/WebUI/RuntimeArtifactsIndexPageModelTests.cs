using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Artifacts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class RuntimeArtifactsIndexPageModelTests
{
    [Fact]
    public async Task OnGetAsync_LoadsSortedAndSearchableArtifacts()
    {
        var repository = new ArtifactRepositoryStub(
            Artifact("sales.sale.created", new SemanticVersion(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready, DateTime.UtcNow.AddHours(-2), checksum: "checksum-old"),
            Artifact("sales.sale.created", new SemanticVersion(1, 1, 0), RuntimeOrchestrationArtifactStatus.Pending, DateTime.UtcNow, checksum: "checksum-new"),
            Artifact("payments.sale.created", new SemanticVersion(2, 0, 0), RuntimeOrchestrationArtifactStatus.Failed, DateTime.UtcNow.AddHours(-1), checksum: "payments-checksum", error: "projection failed"));
        var model = CreateModel(repository);
        model.Search = "sales";

        await model.OnGetAsync();

        Assert.Equal(["1.1.0", "1.0.0"], model.Artifacts.Select(x => x.Version).ToArray());
        Assert.All(model.Artifacts, row => Assert.Equal("sales.sale.created", row.OrchestrationDefinitionKey));
        Assert.Contains(model.Artifacts, row => row.Checksum == "checksum-new" && row.Status == RuntimeOrchestrationArtifactStatus.Pending.ToString());
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
    public void ContainsTreatsNullValuesAsNotMatchingSearchText()
    {
        var method = typeof(IndexModel).GetMethod(
            "Contains",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var result = (bool)method.Invoke(null, [null, "sales"])!;

        Assert.False(result);
    }

    private static IndexModel CreateModel(
        IRuntimeArtifactRepository artifactRepository,
        IRuntimeArtifactReadyNotifier? notifier = null)
    {
        var model = new IndexModel(
            artifactRepository,
            notifier ?? new RecordingRuntimeArtifactReadyNotifier());
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

    private sealed class ArtifactRepositoryStub(params RuntimeOrchestrationArtifact[] artifacts) : IRuntimeArtifactRepository
    {
        private readonly Dictionary<Id, RuntimeOrchestrationArtifact> _artifacts = artifacts.ToDictionary(x => x.Id);

        public bool ThrowOnGetById { get; set; }

        public int GetByIdCalls { get; private set; }

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
            => Task.FromResult<IReadOnlyCollection<RuntimeOrchestrationArtifact>>(_artifacts.Values.ToArray());

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

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }
}
