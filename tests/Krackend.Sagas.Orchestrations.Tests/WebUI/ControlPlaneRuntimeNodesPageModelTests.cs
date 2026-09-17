using System.Reflection;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Primitives;
using RuntimeNodesIndexModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.RuntimeNodes.IndexModel;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ControlPlaneRuntimeNodesPageModelTests
{
    [Fact]
    public async Task OnGetLoadsRuntimeNodes()
    {
        var page = CreatePage([CreateNode("node-1")]);

        await page.OnGetAsync();

        Assert.Single(page.Rows);
        Assert.Equal("node-1", page.Rows.Single().Id);
    }

    [Fact]
    public async Task WizardUpsertCreatesNodeAndReturnsClientPayload()
    {
        var service = new FakeRuntimeNodeApplicationService();
        var page = CreatePage(service);
        page.Input.Name = "Local Runtime";
        page.Input.Code = "local-runtime";
        page.Input.DistributionMode = DistributionMode.HybridSync;
        page.Input.EndpointBaseUri = "https://runtime.local";
        page.Input.Description = "Local runtime node";

        var result = await page.OnPostWizardUpsertAsync();

        Assert.IsType<JsonResult>(result);
        var node = Assert.Single(service.Nodes);
        Assert.Equal("local-runtime", node.Code);
        Assert.Equal(RuntimeNodeStatus.Pending.ToString(), node.Status);
    }

    [Fact]
    public async Task UpsertHandlesInvalidModelStateAndPersistsTrimmedValues()
    {
        var service = new FakeRuntimeNodeApplicationService([CreateNode("node-1")]);
        var page = CreatePage(service);
        page.Input.RuntimeNodeId = "node-1";
        page.Input.Name = " Runtime One ";
        page.Input.Code = " runtime-one ";
        page.Input.DistributionMode = DistributionMode.DesignPublishesToRuntime;
        page.Input.EndpointBaseUri = " https://runtime-one.local ";
        page.Input.Description = " Updated ";

        var update = await page.OnPostUpsertAsync();

        Assert.IsType<RedirectToPageResult>(update);
        Assert.Equal("Runtime One", service.Nodes.Single().Name);
        Assert.Equal("runtime-one", service.Nodes.Single().Code);
        Assert.Equal("https://runtime-one.local", service.Nodes.Single().EndpointBaseUri);

        page.ModelState.AddModelError("Input.Name", "Name is required.");
        var invalid = await page.OnPostUpsertAsync();

        Assert.IsType<PageResult>(invalid);
        Assert.Single(page.Rows);
    }

    [Fact]
    public async Task WizardCredentialActionsGenerateImportAndValidate()
    {
        var node = CreateNode("node-1");
        var connection = new FakeRuntimeNodeConnectionApplicationService
        {
            CredentialPackage = new RuntimeNodeCredentialPackageModel
            {
                Json = """{"kind":"design"}""",
                Base64 = "base64"
            },
            Validation = new RuntimeNodeConnectionValidationModel
            {
                Succeeded = true,
                Message = "ok"
            }
        };
        var page = CreatePage([node], connection);
        AttachPageContext(page);

        var generateResult = await page.OnPostWizardGenerateCredentialsAsync(node.Id);
        page.Credentials.RuntimeNodeId = node.Id;
        page.Credentials.CredentialsJson = """{"kind":"runtime"}""";
        var importResult = await page.OnPostWizardImportCredentialsAsync();
        var validateResult = await page.OnPostWizardValidateConnectionAsync(node.Id);

        Assert.IsType<JsonResult>(generateResult);
        Assert.IsType<JsonResult>(importResult);
        Assert.IsType<JsonResult>(validateResult);
        Assert.Equal("https://design.local", connection.LastIssuerBaseUrl);
        Assert.Equal(node.Id, connection.LastImport.RuntimeNodeId);
    }

    [Fact]
    public async Task WizardRejectsMissingCredentialInputs()
    {
        var page = CreatePage();

        var generate = await page.OnPostWizardGenerateCredentialsAsync("");
        var import = await page.OnPostWizardImportCredentialsAsync();
        var validate = await page.OnPostWizardValidateConnectionAsync("");
        var status = await page.OnPostWizardSetStatusAsync("", RuntimeNodeStatus.Enabled);

        Assert.IsType<BadRequestObjectResult>(generate);
        Assert.IsType<BadRequestObjectResult>(import);
        Assert.IsType<BadRequestObjectResult>(validate);
        Assert.IsType<BadRequestObjectResult>(status);
    }

    [Fact]
    public async Task WizardUpsertReturnsRelevantModelStateErrorsAfterRemovingCredentialsBranch()
    {
        var page = CreatePage();
        page.ModelState.AddModelError("Credentials.CredentialsJson", "Credentials should be ignored.");
        page.ModelState.AddModelError("Input.Name", "Name is required.");

        var result = await page.OnPostWizardUpsertAsync();

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Name is required.", badRequest.Value!.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Credentials should be ignored.", badRequest.Value.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonWizardActionsRedirectAndSetMessages()
    {
        var node = CreateNode("node-1");
        var service = new FakeRuntimeNodeApplicationService([node]);
        var connection = new FakeRuntimeNodeConnectionApplicationService
        {
            Validation = new RuntimeNodeConnectionValidationModel
            {
                Succeeded = false,
                Message = "no route"
            }
        };
        var page = CreatePage(service, connection);
        AttachPageContext(page);

        var statusResult = await page.OnPostSetStatusAsync(node.Id, RuntimeNodeStatus.Suspend);
        var deleteResult = await page.OnPostDeleteAsync(node.Id);
        var validateResult = await page.OnPostValidateConnectionAsync(node.Id);

        Assert.IsType<RedirectToPageResult>(statusResult);
        Assert.IsType<RedirectToPageResult>(deleteResult);
        Assert.IsType<RedirectToPageResult>(validateResult);
        Assert.True(service.Nodes.Single().IsDeleted);
        Assert.Equal("Connection check failed", page.TempData["OrchestratorMessage.Title"]);
    }

    [Fact]
    public async Task NonWizardCredentialsHandleMissingAndSuccessCases()
    {
        var node = CreateNode("node-1");
        var connection = new FakeRuntimeNodeConnectionApplicationService
        {
            CredentialPackage = new RuntimeNodeCredentialPackageModel { Json = """{"secret":"shown"}""" }
        };
        var page = CreatePage([node], connection);
        AttachPageContext(page);

        var missingImport = await page.OnPostImportCredentialsAsync();
        var generate = await page.OnPostGenerateCredentialsAsync(node.Id);
        page.Credentials.RuntimeNodeId = node.Id;
        page.Credentials.CredentialsJson = """{"kind":"runtime"}""";
        var import = await page.OnPostImportCredentialsAsync();

        Assert.IsType<PageResult>(missingImport);
        Assert.IsType<PageResult>(generate);
        Assert.Equal("""{"secret":"shown"}""", page.GeneratedCredentialsJson);
        Assert.Equal("Local Runtime", page.GeneratedCredentialsNodeName);
        Assert.IsType<RedirectToPageResult>(import);
    }

    [Fact]
    public async Task ActionsSurfaceServiceErrorsWithoutThrowing()
    {
        var node = CreateNode("node-1");
        var service = new FakeRuntimeNodeApplicationService([node])
        {
            SetStatusException = new InvalidOperationException("bad transition"),
            DeleteException = new InvalidOperationException("cannot delete")
        };
        var connection = new FakeRuntimeNodeConnectionApplicationService
        {
            GenerateException = new InvalidOperationException("cannot generate"),
            ImportException = new InvalidOperationException("cannot import"),
            ValidateException = new InvalidOperationException("cannot validate")
        };
        var page = CreatePage(service, connection);
        AttachPageContext(page);

        Assert.IsType<RedirectToPageResult>(await page.OnPostSetStatusAsync(node.Id, RuntimeNodeStatus.Enabled));
        Assert.Equal("Runtime node update failed", page.TempData["OrchestratorMessage.Title"]);
        Assert.IsType<RedirectToPageResult>(await page.OnPostDeleteAsync(node.Id));
        Assert.Equal("Runtime node delete failed", page.TempData["OrchestratorMessage.Title"]);
        Assert.IsType<PageResult>(await page.OnPostGenerateCredentialsAsync(node.Id));
        Assert.Equal("Credentials failed", page.TempData["OrchestratorMessage.Title"]);
        page.Credentials.RuntimeNodeId = node.Id;
        page.Credentials.CredentialsJson = "{}";
        Assert.IsType<RedirectToPageResult>(await page.OnPostImportCredentialsAsync());
        Assert.Equal("Credential import failed", page.TempData["OrchestratorMessage.Title"]);
        Assert.IsType<RedirectToPageResult>(await page.OnPostValidateConnectionAsync(node.Id));
        Assert.Equal("Connection check failed", page.TempData["OrchestratorMessage.Title"]);
    }

    [Fact]
    public async Task WizardActionsSurfaceUpsertStatusAndConnectionFailures()
    {
        var node = CreateNode("node-1");
        var service = new FakeRuntimeNodeApplicationService([node])
        {
            UpsertException = new InvalidOperationException("cannot upsert"),
            SetStatusException = new InvalidOperationException("cannot set status")
        };
        var connection = new FakeRuntimeNodeConnectionApplicationService
        {
            GenerateException = new InvalidOperationException("cannot generate"),
            ImportException = new InvalidOperationException("cannot import"),
            ValidateException = new InvalidOperationException("cannot validate")
        };
        var page = CreatePage(service, connection);
        page.Input.Name = "Runtime";
        page.Input.Code = "runtime";
        page.Credentials.RuntimeNodeId = node.Id;
        page.Credentials.CredentialsJson = "{}";

        Assert.IsType<BadRequestObjectResult>(await page.OnPostWizardUpsertAsync());
        Assert.IsType<BadRequestObjectResult>(await page.OnPostWizardGenerateCredentialsAsync(node.Id));
        Assert.IsType<BadRequestObjectResult>(await page.OnPostWizardImportCredentialsAsync());
        Assert.IsType<BadRequestObjectResult>(await page.OnPostWizardValidateConnectionAsync(node.Id));
        Assert.IsType<BadRequestObjectResult>(await page.OnPostWizardSetStatusAsync(node.Id, RuntimeNodeStatus.Suspend));
    }

    [Fact]
    public async Task WizardSetStatusReturnsUpdatedClientNode()
    {
        var node = CreateNode("node-1");
        var service = new FakeRuntimeNodeApplicationService([node]);
        var page = CreatePage(service);

        var result = await page.OnPostWizardSetStatusAsync(node.Id, RuntimeNodeStatus.Suspend);

        Assert.IsType<JsonResult>(result);
        Assert.Equal(RuntimeNodeStatus.Suspend.ToString(), service.Nodes.Single().Status);
    }

    [Fact]
    public void ClientNodeProjectionFormatsLabelsDatesAndDistributionModes()
    {
        var page = CreatePage();
        var node = CreateNode("node-1");
        node.DistributionMode = DistributionMode.DesignPublishesToRuntime.ToString();
        node.Status = "NotActivated";
        node.InboundCredentialCreatedAtUtc = new DateTime(2026, 08, 20, 12, 0, 0, DateTimeKind.Utc);
        node.InboundCredentialRotatedAtUtc = null;
        node.DeletedAtUtc = null;

        var projected = InvokePrivateStatic<object>(typeof(RuntimeNodesIndexModel), "ToClientNode", node);

        Assert.Equal("Design publishes releases", ReadProjected(projected, "distributionModeLabel"));
        Assert.Equal("Design sends approved releases to Runtime using the Runtime credentials.", ReadProjected(projected, "distributionModeDescription"));
        Assert.Equal("Not activated", ReadProjected(projected, "statusLabel"));
        Assert.Equal("2026-08-20 12:00:00", ReadProjected(projected, "inboundCredentialCreatedAt"));
        Assert.Equal("-", ReadProjected(projected, "deletedAt"));
        Assert.Equal(3, page.DistributionModeOptions.Count);
    }

    [Fact]
    public void PrivateLabelHelpersHandleRuntimePullHybridUnknownAndBlankValues()
    {
        Assert.Equal("Runtime imports releases", InvokePrivateStatic<string>(
            typeof(RuntimeNodesIndexModel),
            "DistributionModeLabel",
            DistributionMode.RuntimeFetchesFromDesign.ToString()));
        Assert.Equal("Both directions", InvokePrivateStatic<string>(
            typeof(RuntimeNodesIndexModel),
            "DistributionModeLabel",
            DistributionMode.HybridSync.ToString()));
        Assert.Equal("-", InvokePrivateStatic<string>(
            typeof(RuntimeNodesIndexModel),
            "DistributionModeLabel",
            string.Empty));
        Assert.Equal(string.Empty, InvokePrivateStatic<string>(
            typeof(RuntimeNodesIndexModel),
            "DistributionModeDescription",
            "Other"));
        Assert.Equal("Runtime owners import approved releases using credentials generated by Design.", InvokePrivateStatic<string>(
            typeof(RuntimeNodesIndexModel),
            "DistributionModeDescription",
            DistributionMode.RuntimeFetchesFromDesign.ToString()));
        Assert.Equal("Activation failed", InvokePrivateStatic<string>(
            typeof(RuntimeNodesIndexModel),
            "ReadableLabel",
            "ActivationFailed"));
        Assert.Equal("-", InvokePrivateStatic<string>(
            typeof(RuntimeNodesIndexModel),
            "ReadableLabel",
            string.Empty));
        Assert.Equal("Review the runtime node capture.", InvokePrivate<string>(
            CreatePage(),
            "BuildModelStateMessage"));
    }

    private static RuntimeNodesIndexModel CreatePage(
        IReadOnlyCollection<RuntimeNodeModel>? nodes = null,
        FakeRuntimeNodeConnectionApplicationService? connection = null)
        => CreatePage(new FakeRuntimeNodeApplicationService(nodes ?? Array.Empty<RuntimeNodeModel>()), connection);

    private static RuntimeNodesIndexModel CreatePage(
        FakeRuntimeNodeApplicationService service,
        FakeRuntimeNodeConnectionApplicationService? connection = null)
    {
        var page = new RuntimeNodesIndexModel(service, connection ?? new FakeRuntimeNodeConnectionApplicationService());
        AttachPageContext(page);
        return page;
    }

    private static void AttachPageContext(PageModel page)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("design.local");
        httpContext.Request.Headers.Host = new StringValues("design.local");
        page.PageContext = new PageContext { HttpContext = httpContext };
        page.TempData = new TempDataDictionary(httpContext, new InMemoryTempDataProvider());
    }

    private static RuntimeNodeModel CreateNode(string id)
        => new()
        {
            Id = id,
            Name = "Local Runtime",
            Code = "local-runtime",
            DistributionMode = DistributionMode.HybridSync.ToString(),
            EndpointBaseUri = "https://runtime.local",
            EndpointApiPath = "/api/orchestrator/runtime/artifacts",
            Status = RuntimeNodeStatus.Enabled.ToString(),
            InboundCredentialStatus = "Active",
            OutboundCredentialStatus = "Active",
            AccessTokenTtlSeconds = 86_400,
            TokenRefreshSkewSeconds = 300,
            TokenValidationCacheTtlSeconds = 300,
            InboundClientId = "runtime-client",
            InboundKeyId = "runtime-key",
            InboundAllowedScopes = "artifact:push",
            OutboundClientId = "design-client",
            OutboundKeyId = "design-key",
            OutboundRequestedScopes = "artifact:pull",
            IsEnabled = true,
            Description = "Node",
            RegisteredAtUtc = DateTime.UtcNow.AddHours(-1),
            LastUpdatedAtUtc = DateTime.UtcNow
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

    private sealed class FakeRuntimeNodeApplicationService : IRuntimeNodeApplicationService
    {
        private readonly List<RuntimeNodeModel> _nodes;

        public FakeRuntimeNodeApplicationService(IReadOnlyCollection<RuntimeNodeModel>? nodes = null)
        {
            _nodes = (nodes ?? Array.Empty<RuntimeNodeModel>()).ToList();
        }

        public Exception? SetStatusException { get; set; }

        public Exception? DeleteException { get; set; }

        public Exception? UpsertException { get; set; }

        public IReadOnlyCollection<RuntimeNodeModel> Nodes => _nodes;

        public Task<string> Upsert(UpsertRuntimeNodeInput input, CancellationToken cancellationToken = default)
        {
            if (UpsertException is not null)
            {
                throw UpsertException;
            }

            var id = string.IsNullOrWhiteSpace(input.RuntimeNodeId) ? $"node-{_nodes.Count + 1}" : input.RuntimeNodeId;
            var existing = _nodes.FirstOrDefault(x => x.Id == id);
            var node = existing ?? CreateNode(id);
            node.Name = input.Name;
            node.Code = input.Code;
            node.DistributionMode = input.DistributionMode.ToString();
            node.EndpointBaseUri = input.EndpointBaseUri;
            node.Description = input.Description;
            node.Status = existing?.Status ?? RuntimeNodeStatus.Pending.ToString();

            if (existing is null)
            {
                _nodes.Add(node);
            }

            return Task.FromResult(id);
        }

        public Task SetStatus(string runtimeNodeId, RuntimeNodeStatus status, CancellationToken cancellationToken = default)
        {
            if (SetStatusException is not null)
            {
                throw SetStatusException;
            }

            _nodes.First(x => x.Id == runtimeNodeId).Status = status.ToString();
            return Task.CompletedTask;
        }

        public Task Delete(string runtimeNodeId, CancellationToken cancellationToken = default)
        {
            if (DeleteException is not null)
            {
                throw DeleteException;
            }

            _nodes.First(x => x.Id == runtimeNodeId).IsDeleted = true;
            return Task.CompletedTask;
        }

        public Task<ApplicationPagedResult<RuntimeNodeModel>> GetAll(
            ApplicationPagedSettings settings,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ApplicationPagedResult<RuntimeNodeModel>
            {
                PageNumber = settings.PageNumber,
                PageSize = settings.PageSize,
                TotalRows = _nodes.Count,
                TotalPages = 1,
                Rows = _nodes.ToArray()
            });
    }

    private sealed class FakeRuntimeNodeConnectionApplicationService : IRuntimeNodeConnectionApplicationService
    {
        public RuntimeNodeCredentialPackageModel CredentialPackage { get; set; } = new()
        {
            Json = "{}",
            Base64 = string.Empty
        };

        public RuntimeNodeConnectionValidationModel Validation { get; set; } = new()
        {
            Succeeded = true,
            Message = "ok"
        };

        public Exception? GenerateException { get; set; }

        public Exception? ImportException { get; set; }

        public Exception? ValidateException { get; set; }

        public string LastIssuerBaseUrl { get; private set; } = string.Empty;

        public ImportRuntimeNodeCredentialPackageInput LastImport { get; private set; } = new();

        public Task<RuntimeNodeCredentialPackageModel> GenerateCredentialPackage(
            string runtimeNodeId,
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

        public Task ImportCredentialPackage(
            ImportRuntimeNodeCredentialPackageInput input,
            CancellationToken cancellationToken = default)
        {
            if (ImportException is not null)
            {
                throw ImportException;
            }

            LastImport = input;
            return Task.CompletedTask;
        }

        public Task<RuntimeNodeConnectionValidationModel> ValidateConnection(
            string runtimeNodeId,
            CancellationToken cancellationToken = default)
        {
            if (ValidateException is not null)
            {
                throw ValidateException;
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
