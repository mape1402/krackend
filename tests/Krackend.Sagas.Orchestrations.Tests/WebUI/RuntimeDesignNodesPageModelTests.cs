using System.Reflection;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Primitives;
using RuntimeDesignNodesIndexModel = Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.DesignNodes.IndexModel;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class RuntimeDesignNodesPageModelTests
{
    [Fact]
    public async Task OnGetLoadsNodesAndPendingArtifactsForSelectedSource()
    {
        var node = CreateConfiguredNode();
        var page = CreatePage([node], pending: [CreatePackage("target-1")]);
        page.SourceKey = node.Key;

        await page.OnGetAsync();

        Assert.Single(page.DesignNodes);
        Assert.Single(page.PendingArtifacts);
        Assert.Equal("target-1", page.PendingArtifacts.Single().ReleaseTargetId);
    }

    [Fact]
    public async Task WizardUpsertCreatesPendingNodeAndWizardPolicyUpdatesDistributionMode()
    {
        var repo = new FakeRuntimeDesignNodeRepository();
        var page = CreatePage(repo);
        page.Input.Key = "design-main";
        page.Input.Name = "Design Main";
        page.Input.Description = "Primary design node";

        var upsertResult = await page.OnPostWizardUpsertAsync();

        var json = Assert.IsType<JsonResult>(upsertResult);
        Assert.Contains("Basic data saved", json.Value!.ToString(), StringComparison.Ordinal);
        var node = repo.Nodes.Single();
        Assert.Equal(RuntimeDesignNodeStatus.Pending, node.Status);

        page.Policy.DesignNodeId = node.Id.ToString();
        page.Policy.DistributionMode = DistributionConnectionMode.RuntimeFetchesFromDesign;

        var policyResult = await page.OnPostWizardPolicyAsync();

        Assert.IsType<JsonResult>(policyResult);
        Assert.Equal(DistributionConnectionMode.RuntimeFetchesFromDesign, repo.Nodes.Single().DistributionMode);
    }

    [Fact]
    public async Task UpsertCreatesAndUpdatesBasicDataWithDefaults()
    {
        var repo = new FakeRuntimeDesignNodeRepository();
        var page = CreatePage(repo);
        page.Input.Key = "design-main";
        page.Input.Name = " Design Main ";
        page.Input.Description = " Primary node ";

        var createResult = await page.OnPostUpsertAsync();

        var node = repo.Nodes.Single();
        Assert.IsType<RedirectToPageResult>(createResult);
        Assert.Equal("Design Main", node.Name);
        Assert.Equal("Primary node", node.Description);
        Assert.Equal(DistributionConnectionMode.HybridSync, node.DistributionMode);
        Assert.Equal(86_400, node.AccessTokenTtlSeconds);
        Assert.Equal(RuntimeDesignNodeStatus.Pending, node.Status);

        page.Input.DesignNodeId = node.Id.ToString();
        page.Input.Key = "design-main";
        page.Input.Name = "Design Renamed";
        page.Input.Description = "Updated";

        var updateResult = await page.OnPostUpsertAsync();

        Assert.IsType<RedirectToPageResult>(updateResult);
        Assert.Equal("Design Renamed", repo.Nodes.Single().Name);
        Assert.Equal("Updated", repo.Nodes.Single().Description);
    }

    [Fact]
    public async Task UpsertReturnsPageAndReloadsNodesWhenModelStateIsInvalid()
    {
        var existing = CreateConfiguredNode();
        var page = CreatePage([existing]);
        page.ModelState.AddModelError("Input.Key", "Key is required.");

        var result = await page.OnPostUpsertAsync();

        Assert.IsType<PageResult>(result);
        Assert.Single(page.DesignNodes);
    }

    [Fact]
    public async Task WizardPolicyMovesEnabledNodeBackToPendingWhenSelectedModeIsNotConfigured()
    {
        var node = CreatePushOnlyConfiguredNode();
        var repo = new FakeRuntimeDesignNodeRepository([node]);
        var page = CreatePage(repo);
        page.Policy.DesignNodeId = node.Id.ToString();
        page.Policy.DistributionMode = DistributionConnectionMode.RuntimeFetchesFromDesign;

        var result = await page.OnPostWizardPolicyAsync();

        Assert.IsType<JsonResult>(result);
        Assert.Equal(DistributionConnectionMode.RuntimeFetchesFromDesign, node.DistributionMode);
        Assert.Equal(RuntimeDesignNodeStatus.Pending, node.Status);
        Assert.False(node.IsEnabled);
    }

    [Fact]
    public async Task WizardCredentialActionsGenerateImportAndValidateConnection()
    {
        var node = CreateConfiguredNode();
        var connection = new FakeRuntimeDesignNodeConnectionService
        {
            CredentialPackage = new RuntimeDesignNodeCredentialPackageModel
            {
                Json = """{"kind":"runtime"}""",
                Base64 = "base64"
            },
            Validation = new RuntimeDesignNodeConnectionValidationModel
            {
                Succeeded = true,
                Message = "ok"
            }
        };
        var page = CreatePage([node], connection: connection);
        AttachPageContext(page);

        var generateResult = await page.OnPostWizardGenerateCredentialsAsync(node.Id.ToString());
        page.CredentialPackage.DesignNodeId = node.Id.ToString();
        page.CredentialPackage.CredentialsJson = """{"kind":"design"}""";
        var importResult = await page.OnPostWizardImportCredentialsAsync();
        var validateResult = await page.OnPostWizardValidateConnectionAsync(node.Id.ToString());

        Assert.IsType<JsonResult>(generateResult);
        Assert.IsType<JsonResult>(importResult);
        Assert.IsType<JsonResult>(validateResult);
        Assert.Equal("https://runtime.local", connection.LastIssuerBaseUrl);
        Assert.Equal(node.Id.ToString(), connection.LastImport.DesignNodeId);
    }

    [Fact]
    public async Task WizardRejectsMissingInputsWithBadRequest()
    {
        var page = CreatePage();

        var generate = await page.OnPostWizardGenerateCredentialsAsync("");
        var import = await page.OnPostWizardImportCredentialsAsync();
        var validate = await page.OnPostWizardValidateConnectionAsync("");
        var setStatus = await page.OnPostWizardSetStatusAsync("", RuntimeDesignNodeStatus.Enabled);

        Assert.IsType<BadRequestObjectResult>(generate);
        Assert.IsType<BadRequestObjectResult>(import);
        Assert.IsType<BadRequestObjectResult>(validate);
        Assert.IsType<BadRequestObjectResult>(setStatus);
    }

    [Fact]
    public async Task WizardPostsReturnRelevantModelStateErrorsAfterRemovingUnrelatedBranches()
    {
        var page = CreatePage();
        page.ModelState.AddModelError("Policy.DistributionMode", "Policy should be ignored.");
        page.ModelState.AddModelError("Input.DesignNodeId", "Existing id should be ignored.");
        page.ModelState.AddModelError("Input.Name", "Name is required.");

        var upsertResult = await page.OnPostWizardUpsertAsync();

        var upsertBadRequest = Assert.IsType<BadRequestObjectResult>(upsertResult);
        Assert.Contains("Name is required.", upsertBadRequest.Value!.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Policy should be ignored.", upsertBadRequest.Value.ToString(), StringComparison.Ordinal);

        var policyPage = CreatePage([CreateConfiguredNode()]);
        policyPage.ModelState.AddModelError("Input.Name", "Input should be ignored.");
        policyPage.ModelState.AddModelError("CredentialPackage.CredentialsJson", "Credentials should be ignored.");
        policyPage.ModelState.AddModelError("Policy.DistributionMode", "Policy mode is required.");

        var policyResult = await policyPage.OnPostWizardPolicyAsync();

        var policyBadRequest = Assert.IsType<BadRequestObjectResult>(policyResult);
        Assert.Contains("Policy mode is required.", policyBadRequest.Value!.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Input should be ignored.", policyBadRequest.Value.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatusTransitionsHonorConfigurationRules()
    {
        var pendingNode = CreateNode(RuntimeDesignNodeStatus.Pending);
        var page = CreatePage([pendingNode]);
        AttachPageContext(page);

        var enableResult = await page.OnPostWizardSetStatusAsync(pendingNode.Id.ToString(), RuntimeDesignNodeStatus.Enabled);
        var suspendResult = await page.OnPostWizardSetStatusAsync(pendingNode.Id.ToString(), RuntimeDesignNodeStatus.Suspend);

        Assert.IsType<BadRequestObjectResult>(enableResult);
        Assert.IsType<JsonResult>(suspendResult);
        Assert.Equal(RuntimeDesignNodeStatus.Suspend, pendingNode.Status);
    }

    [Fact]
    public async Task PullActionsHandleEmptyRejectedAndAcceptedReleases()
    {
        var node = CreateConfiguredNode();
        var pull = new FakeControlPlaneArtifactPullService
        {
            Pending = [CreatePackage("target-1")],
            ApplyResult = new RuntimeArtifactDeploymentResult
            {
                Accepted = true,
                Message = "accepted",
                RuntimeArtifactId = "runtime-artifact",
                Status = "Ready"
            }
        };
        var page = CreatePage([node], pull: pull);
        AttachPageContext(page);

        var missingResult = await page.OnPostApplyReleaseAsync("", "");
        var checkResult = await page.OnPostCheckPendingAsync(node.Key);
        var applyResult = await page.OnPostApplyReleaseAsync(node.Key, "target-1");

        Assert.IsType<PageResult>(missingResult);
        Assert.IsType<PageResult>(checkResult);
        Assert.Single(page.PendingArtifacts);
        Assert.IsType<RedirectToPageResult>(applyResult);
        Assert.Equal("target-1", pull.LastReleaseTargetId);
    }

    [Fact]
    public async Task PullActionsSurfaceFailuresWithoutThrowing()
    {
        var node = CreateConfiguredNode();
        var page = CreatePage([node], pull: new FakeControlPlaneArtifactPullService
        {
            PendingException = new InvalidOperationException("source unavailable"),
            ApplyException = new InvalidOperationException("apply failed")
        });
        AttachPageContext(page);

        var checkResult = await page.OnPostCheckPendingAsync(node.Key);
        var applyResult = await page.OnPostApplyReleaseAsync(node.Key, "target-1");

        Assert.IsType<PageResult>(checkResult);
        Assert.Equal("source unavailable", page.ErrorMessage);
        Assert.IsType<RedirectToPageResult>(applyResult);
    }

    [Fact]
    public async Task NonWizardPostsRedirectAndSetTempDataMessages()
    {
        var node = CreateConfiguredNode();
        var connection = new FakeRuntimeDesignNodeConnectionService
        {
            Validation = new RuntimeDesignNodeConnectionValidationModel { Succeeded = false, Message = "no route" }
        };
        var page = CreatePage([node], connection: connection);
        AttachPageContext(page);

        var validateResult = await page.OnPostValidateConnectionAsync(node.Id.ToString());
        var statusResult = await page.OnPostSetStatusAsync(node.Id.ToString(), RuntimeDesignNodeStatus.Suspend);

        Assert.IsType<RedirectToPageResult>(validateResult);
        Assert.IsType<RedirectToPageResult>(statusResult);
        Assert.Equal("Design node updated", page.TempData["OrchestratorMessage.Title"]);
    }

    [Fact]
    public async Task NonWizardActionsSurfaceConnectionAndStatusErrorsInTempData()
    {
        var node = CreateNode(RuntimeDesignNodeStatus.Pending);
        var connection = new FakeRuntimeDesignNodeConnectionService
        {
            ValidationException = new InvalidOperationException("network down")
        };
        var page = CreatePage([node], connection: connection);
        AttachPageContext(page);

        var validateResult = await page.OnPostValidateConnectionAsync(node.Id.ToString());

        Assert.IsType<RedirectToPageResult>(validateResult);
        Assert.Equal("Connection check failed", page.TempData["OrchestratorMessage.Title"]);
        Assert.Equal("network down", page.TempData["OrchestratorMessage.Body"]);
        var statusResult = await page.OnPostSetStatusAsync(node.Id.ToString(), RuntimeDesignNodeStatus.Enabled);

        Assert.IsType<RedirectToPageResult>(statusResult);
        Assert.Equal("Design node update failed", page.TempData["OrchestratorMessage.Title"]);
        Assert.Contains("Complete the required credentials", page.TempData["OrchestratorMessage.Body"]!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WizardActionsReturnBadRequestWhenNodeIdIsInvalidOrServicesFail()
    {
        var node = CreateConfiguredNode();
        var connection = new FakeRuntimeDesignNodeConnectionService
        {
            GenerateException = new InvalidOperationException("cannot generate"),
            ImportException = new InvalidOperationException("cannot import"),
            ValidationException = new InvalidOperationException("cannot validate")
        };
        var page = CreatePage([node], connection: connection);

        var invalidPolicyResult = await page.OnPostWizardPolicyAsync();
        var invalidStatusResult = await page.OnPostWizardSetStatusAsync("not-a-node-id", RuntimeDesignNodeStatus.Suspend);
        var generateResult = await page.OnPostWizardGenerateCredentialsAsync(node.Id.ToString());
        page.CredentialPackage.DesignNodeId = node.Id.ToString();
        page.CredentialPackage.CredentialsJson = """{"kind":"design"}""";
        var importResult = await page.OnPostWizardImportCredentialsAsync();
        var validateResult = await page.OnPostWizardValidateConnectionAsync(node.Id.ToString());

        Assert.IsType<BadRequestObjectResult>(invalidPolicyResult);
        Assert.IsType<BadRequestObjectResult>(invalidStatusResult);
        Assert.IsType<BadRequestObjectResult>(generateResult);
        Assert.IsType<BadRequestObjectResult>(importResult);
        Assert.IsType<BadRequestObjectResult>(validateResult);
    }

    [Fact]
    public async Task WizardUpsertRejectsInvalidExistingIdAndPreservesModelStateFallbackMessage()
    {
        var page = CreatePage();
        page.Input.DesignNodeId = "not-a-node-id";
        page.Input.Key = "design-main";
        page.Input.Name = "Design Main";

        var result = await page.OnPostWizardUpsertAsync();

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Design node id is invalid", badRequest.Value!.ToString(), StringComparison.Ordinal);
        Assert.Equal("Review the design node capture.", InvokePrivate<string>(page, "BuildModelStateMessage"));
    }

    [Fact]
    public async Task UpsertMovesEnabledNodeBackToPendingWhenConfigurationIsNoLongerComplete()
    {
        var node = CreateNode(RuntimeDesignNodeStatus.Enabled);
        node.IsEnabled = true;
        node.InboundCredentialStatus = ConnectionCredentialStatus.Active;
        node.OutboundCredentialStatus = ConnectionCredentialStatus.Missing;
        node.DistributionMode = DistributionConnectionMode.HybridSync;
        var repo = new FakeRuntimeDesignNodeRepository([node]);
        var page = CreatePage(repo);
        page.Input.DesignNodeId = node.Id.ToString();
        page.Input.Key = "design-main";
        page.Input.Name = "Design Main";
        page.Input.Description = "Still incomplete";

        var result = await page.OnPostWizardUpsertAsync();

        Assert.IsType<JsonResult>(result);
        Assert.Equal(RuntimeDesignNodeStatus.Pending, repo.Nodes.Single().Status);
        Assert.False(repo.Nodes.Single().IsEnabled);
    }

    [Fact]
    public async Task StatusTransitionsAllowConfiguredPendingAndSuspendedNodes()
    {
        var pending = CreateConfiguredNode();
        pending.Status = RuntimeDesignNodeStatus.Pending;
        pending.IsEnabled = false;
        var suspended = CreateConfiguredNode();
        suspended.Id = Id.New();
        suspended.Key = "design-suspended";
        suspended.Status = RuntimeDesignNodeStatus.Suspend;
        suspended.IsEnabled = false;
        var page = CreatePage([pending, suspended]);

        var enablePending = await page.OnPostWizardSetStatusAsync(pending.Id.ToString(), RuntimeDesignNodeStatus.Enabled);
        var reopenSuspended = await page.OnPostWizardSetStatusAsync(suspended.Id.ToString(), RuntimeDesignNodeStatus.Pending);
        suspended.Status = RuntimeDesignNodeStatus.Suspend;
        var enableSuspended = await page.OnPostWizardSetStatusAsync(suspended.Id.ToString(), RuntimeDesignNodeStatus.Enabled);
        var invalidBackwards = await page.OnPostWizardSetStatusAsync(pending.Id.ToString(), RuntimeDesignNodeStatus.Pending);

        Assert.IsType<JsonResult>(enablePending);
        Assert.IsType<JsonResult>(reopenSuspended);
        Assert.IsType<JsonResult>(enableSuspended);
        Assert.IsType<BadRequestObjectResult>(invalidBackwards);
    }

    [Fact]
    public void ClientNodeProjectionFormatsModesStatusesDatesAndUnknownLabels()
    {
        var page = CreatePage();
        var node = CreateConfiguredNode();
        node.DistributionMode = DistributionConnectionMode.DesignPublishesToRuntime;
        node.Status = RuntimeDesignNodeStatus.Enabled;
        node.InboundCredentialCreatedAtUtc = new DateTime(2026, 08, 20, 12, 0, 0, DateTimeKind.Utc);
        node.InboundCredentialRotatedAtUtc = null;
        node.OutboundCredentialImportedAtUtc = null;

        var projected = InvokePrivateStatic<object>(typeof(RuntimeDesignNodesIndexModel), "ToClientNode", node);

        Assert.Equal("Design pushes to Runtime", ReadProjected(projected, "distributionModeLabel"));
        Assert.Equal("Design publishes approved releases into this Runtime node.", ReadProjected(projected, "distributionModeDescription"));
        Assert.Equal("Enabled", ReadProjected(projected, "statusLabel"));
        Assert.Equal("2026-08-20 12:00:00", ReadProjected(projected, "inboundCredentialCreatedAt"));
        Assert.Equal("-", ReadProjected(projected, "outboundCredentialImportedAt"));
        Assert.Equal("False", ReadProjected(projected, "canCheckConnection"));
        Assert.Equal(3, page.DistributionModeOptions.Count);
    }

    [Fact]
    public void PrivateLabelHelpersHandleRuntimePullHybridAndBlankValues()
    {
        Assert.Equal("Runtime pulls from Design", InvokePrivateStatic<string>(
            typeof(RuntimeDesignNodesIndexModel),
            "DistributionModeLabel",
            DistributionConnectionMode.RuntimeFetchesFromDesign.ToString()));
        Assert.Equal("Hybrid", InvokePrivateStatic<string>(
            typeof(RuntimeDesignNodesIndexModel),
            "DistributionModeLabel",
            DistributionConnectionMode.HybridSync.ToString()));
        Assert.Equal("-", InvokePrivateStatic<string>(
            typeof(RuntimeDesignNodesIndexModel),
            "DistributionModeLabel",
            string.Empty));
        Assert.Equal(string.Empty, InvokePrivateStatic<string>(
            typeof(RuntimeDesignNodesIndexModel),
            "DistributionModeDescription",
            "Other"));
        Assert.Equal("Design pushes to Runtime", InvokePrivateStatic<string>(
            typeof(RuntimeDesignNodesIndexModel),
            "ReadableLabel",
            DistributionConnectionMode.DesignPublishesToRuntime.ToString()));
        Assert.Equal("Runtime pulls from Design", InvokePrivateStatic<string>(
            typeof(RuntimeDesignNodesIndexModel),
            "ReadableLabel",
            DistributionConnectionMode.RuntimeFetchesFromDesign.ToString()));
        Assert.Equal("Hybrid", InvokePrivateStatic<string>(
            typeof(RuntimeDesignNodesIndexModel),
            "ReadableLabel",
            DistributionConnectionMode.HybridSync.ToString()));
    }

    private static RuntimeDesignNodesIndexModel CreatePage(
        IReadOnlyCollection<RuntimeDesignNode>? nodes = null,
        IReadOnlyCollection<RuntimeArtifactDeliveryPackage>? pending = null,
        FakeControlPlaneArtifactPullService? pull = null,
        FakeRuntimeDesignNodeConnectionService? connection = null)
    {
        var repo = new FakeRuntimeDesignNodeRepository(nodes ?? Array.Empty<RuntimeDesignNode>());
        return CreatePage(repo, pull, connection, pending);
    }

    private static RuntimeDesignNodesIndexModel CreatePage(
        FakeRuntimeDesignNodeRepository repo,
        FakeControlPlaneArtifactPullService? pull = null,
        FakeRuntimeDesignNodeConnectionService? connection = null,
        IReadOnlyCollection<RuntimeArtifactDeliveryPackage>? pending = null)
    {
        var page = new RuntimeDesignNodesIndexModel(
            repo,
            pull ?? new FakeControlPlaneArtifactPullService { Pending = pending ?? Array.Empty<RuntimeArtifactDeliveryPackage>() },
            connection ?? new FakeRuntimeDesignNodeConnectionService());
        AttachPageContext(page);
        return page;
    }

    private static void AttachPageContext(PageModel page)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("runtime.local");
        httpContext.Request.Headers.Host = new StringValues("runtime.local");
        page.PageContext = new PageContext { HttpContext = httpContext };
        page.TempData = new TempDataDictionary(httpContext, new InMemoryTempDataProvider());
    }

    private static RuntimeDesignNode CreateConfiguredNode()
        => new()
        {
            Id = Id.New(),
            Key = "design-main",
            Name = "Design Main",
            EndpointBaseUri = "https://design.local",
            RemoteRuntimeNodeId = "runtime-1",
            DistributionMode = DistributionConnectionMode.HybridSync,
            InboundCredentialStatus = ConnectionCredentialStatus.Active,
            OutboundCredentialStatus = ConnectionCredentialStatus.Active,
            Status = RuntimeDesignNodeStatus.Enabled,
            IsEnabled = true,
            CreatedOnUtc = DateTime.UtcNow.AddMinutes(-10),
            UpdatedOnUtc = DateTime.UtcNow
        };

    private static RuntimeDesignNode CreatePushOnlyConfiguredNode()
        => new()
        {
            Id = Id.New(),
            Key = "design-push",
            Name = "Design Push",
            DistributionMode = DistributionConnectionMode.DesignPublishesToRuntime,
            InboundCredentialStatus = ConnectionCredentialStatus.Active,
            OutboundCredentialStatus = ConnectionCredentialStatus.Missing,
            Status = RuntimeDesignNodeStatus.Enabled,
            IsEnabled = true,
            AccessTokenTtlSeconds = 86_400,
            TokenRefreshSkewSeconds = 300,
            TokenValidationCacheTtlSeconds = 300,
            CreatedOnUtc = DateTime.UtcNow.AddMinutes(-10),
            UpdatedOnUtc = DateTime.UtcNow
        };

    private static RuntimeDesignNode CreateNode(RuntimeDesignNodeStatus status)
        => new()
        {
            Id = Id.New(),
            Key = "design-pending",
            Name = "Design Pending",
            DistributionMode = DistributionConnectionMode.HybridSync,
            Status = status,
            IsEnabled = status == RuntimeDesignNodeStatus.Enabled,
            CreatedOnUtc = DateTime.UtcNow.AddMinutes(-10),
            UpdatedOnUtc = DateTime.UtcNow
        };

    private static TResult InvokePrivate<TResult>(object target, string methodName)
        => (TResult)target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(target, Array.Empty<object>())!;

    private static TResult InvokePrivateStatic<TResult>(Type type, string methodName, params object[] parameters)
        => (TResult)type
            .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, parameters)!;

    private static string ReadProjected(object target, string propertyName)
        => target.GetType().GetProperty(propertyName)!.GetValue(target)!.ToString()!;

    private static RuntimeArtifactDeliveryPackage CreatePackage(string releaseTargetId)
        => new()
        {
            ReleaseTargetId = releaseTargetId,
            ArtifactId = "artifact-1",
            ArtifactType = "orchestration-version-snapshot",
            SchemaVersion = "1.0.0",
            OrchestrationDefinitionId = "definition-1",
            OrchestrationVersionId = "version-1",
            OrchestrationDefinitionKey = "sales.sale.created",
            Version = "1.0.0",
            Checksum = "checksum",
            PayloadJson = "{}",
            CorrelationId = "correlation",
            PromotedBy = "tester",
            PromotedOnUtc = DateTime.UtcNow
        };

    private sealed class FakeRuntimeDesignNodeRepository : IRuntimeDesignNodeRepository
    {
        private readonly Dictionary<Id, RuntimeDesignNode> _nodes;

        public FakeRuntimeDesignNodeRepository(IReadOnlyCollection<RuntimeDesignNode>? nodes = null)
        {
            _nodes = (nodes ?? Array.Empty<RuntimeDesignNode>()).ToDictionary(x => x.Id);
        }

        public IReadOnlyCollection<RuntimeDesignNode> Nodes => _nodes.Values.ToArray();

        public Task<IReadOnlyCollection<RuntimeDesignNode>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<RuntimeDesignNode>)_nodes.Values.ToArray());

        public IReadOnlyCollection<RuntimeDesignNode> GetEnabled()
            => _nodes.Values.Where(x => x.IsEnabled).ToArray();

        public Task<RuntimeDesignNode> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
            => Task.FromResult(_nodes.GetValueOrDefault(id)!);

        public Task<RuntimeDesignNode> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_nodes.Values.FirstOrDefault(x => x.Key == key)!);

        public Task<RuntimeDesignNode> GetByInboundClientIdAsync(string clientId, CancellationToken cancellationToken = default)
            => Task.FromResult(_nodes.Values.FirstOrDefault(x => x.InboundClientId == clientId)!);

        public Task UpsertAsync(RuntimeDesignNode designNode, CancellationToken cancellationToken = default)
        {
            _nodes[designNode.Id] = designNode;
            return Task.CompletedTask;
        }

        public Task SetEnabledAsync(Id id, bool isEnabled, CancellationToken cancellationToken = default)
        {
            _nodes[id].IsEnabled = isEnabled;
            return Task.CompletedTask;
        }

        public Task SetStatusAsync(Id id, RuntimeDesignNodeStatus status, CancellationToken cancellationToken = default)
        {
            _nodes[id].Status = status;
            _nodes[id].IsEnabled = status == RuntimeDesignNodeStatus.Enabled;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeControlPlaneArtifactPullService : IControlPlaneArtifactPullService
    {
        public IReadOnlyCollection<RuntimeArtifactDeliveryPackage> Pending { get; set; } = Array.Empty<RuntimeArtifactDeliveryPackage>();

        public RuntimeArtifactDeploymentResult ApplyResult { get; set; } = new()
        {
            Accepted = false,
            Message = "rejected",
            Status = "Rejected"
        };

        public Exception? PendingException { get; set; }

        public Exception? ApplyException { get; set; }

        public string LastReleaseTargetId { get; private set; } = string.Empty;

        public IReadOnlyCollection<ControlPlaneDistributionSource> GetSources()
            => Array.Empty<ControlPlaneDistributionSource>();

        public Task<IReadOnlyCollection<RuntimeArtifactDeliveryPackage>> GetPendingAsync(
            string sourceKey,
            CancellationToken cancellationToken = default)
        {
            if (PendingException is not null)
            {
                throw PendingException;
            }

            return Task.FromResult(Pending);
        }

        public Task<RuntimeArtifactDeploymentResult> ApplyAsync(
            string sourceKey,
            string releaseTargetId,
            CancellationToken cancellationToken = default)
        {
            LastReleaseTargetId = releaseTargetId;
            if (ApplyException is not null)
            {
                throw ApplyException;
            }

            return Task.FromResult(ApplyResult);
        }
    }

    private sealed class FakeRuntimeDesignNodeConnectionService : IRuntimeDesignNodeConnectionService
    {
        public RuntimeDesignNodeCredentialPackageModel CredentialPackage { get; set; } = new()
        {
            Json = "{}",
            Base64 = string.Empty
        };

        public RuntimeDesignNodeConnectionValidationModel Validation { get; set; } = new()
        {
            Succeeded = true,
            Message = "ok"
        };

        public Exception? GenerateException { get; set; }

        public Exception? ImportException { get; set; }

        public Exception? ValidationException { get; set; }

        public string LastIssuerBaseUrl { get; private set; } = string.Empty;

        public ImportRuntimeDesignNodeCredentialPackageInput LastImport { get; private set; } = new();

        public Task<RuntimeDesignNodeCredentialPackageModel> GenerateCredentialPackageAsync(
            string designNodeId,
            string issuerBaseUrl,
            CancellationToken cancellationToken = default)
        {
            if (GenerateException is not null)
            {
                throw GenerateException;
            }

            LastIssuerBaseUrl = issuerBaseUrl;
            return Task.FromResult(CredentialPackage);
        }

        public Task ImportCredentialPackageAsync(
            ImportRuntimeDesignNodeCredentialPackageInput input,
            CancellationToken cancellationToken = default)
        {
            if (ImportException is not null)
            {
                throw ImportException;
            }

            LastImport = input;
            return Task.CompletedTask;
        }

        public Task<RuntimeDesignNodeConnectionValidationModel> ValidateConnectionAsync(
            string designNodeId,
            CancellationToken cancellationToken = default)
        {
            if (ValidationException is not null)
            {
                throw ValidationException;
            }

            return Task.FromResult(Validation);
        }
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
