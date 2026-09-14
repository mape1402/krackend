using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.DesignNodes;

/// <summary>
/// Manages design/control-plane nodes registered in the runtime.
/// </summary>
public sealed class IndexModel : PageModel
{
    private readonly IRuntimeDesignNodeRepository _repository;
    private readonly IControlPlaneArtifactPullService _pullService;
    private readonly IRuntimeDesignNodeConnectionService _connectionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    public IndexModel(
        IRuntimeDesignNodeRepository repository,
        IControlPlaneArtifactPullService pullService,
        IRuntimeDesignNodeConnectionService connectionService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _pullService = pullService ?? throw new ArgumentNullException(nameof(pullService));
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
    }

    /// <summary>
    /// Gets registered design nodes.
    /// </summary>
    public IReadOnlyCollection<RuntimeDesignNode> DesignNodes { get; private set; } = Array.Empty<RuntimeDesignNode>();

    /// <summary>
    /// Gets pending artifacts for the selected design node.
    /// </summary>
    public IReadOnlyCollection<PendingArtifactViewModel> PendingArtifacts { get; private set; } = Array.Empty<PendingArtifactViewModel>();

    /// <summary>
    /// Gets distribution mode options displayed by the setup wizard.
    /// </summary>
    public IReadOnlyCollection<RuntimeDesignNodeDistributionModeOption> DistributionModeOptions { get; } =
    [
        new RuntimeDesignNodeDistributionModeOption
        {
            Value = DistributionConnectionMode.DesignPublishesToRuntime.ToString(),
            Label = "Design pushes to Runtime",
            Description = "Design publishes approved releases into this Runtime node."
        },
        new RuntimeDesignNodeDistributionModeOption
        {
            Value = DistributionConnectionMode.RuntimeFetchesFromDesign.ToString(),
            Label = "Runtime pulls from Design",
            Description = "Runtime owners pull approved releases manually from Design."
        },
        new RuntimeDesignNodeDistributionModeOption
        {
            Value = DistributionConnectionMode.HybridSync.ToString(),
            Label = "Hybrid",
            Description = "Design can push and Runtime can pull releases."
        }
    ];

    /// <summary>
    /// Gets or sets the selected design node key.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets form input.
    /// </summary>
    [BindProperty]
    public DesignNodeInput Input { get; set; } = new();

    /// <summary>
    /// Gets or sets setup policy input.
    /// </summary>
    [BindProperty]
    public DesignNodePolicyInput Policy { get; set; } = new();

    /// <summary>
    /// Gets or sets the credential package import input.
    /// </summary>
    [BindProperty]
    public RuntimeDesignNodeCredentialPackageInput CredentialPackage { get; set; } = new();

    /// <summary>
    /// Gets a generated credential JSON value.
    /// </summary>
    public string GeneratedCredentialJson { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the node name associated with the generated credentials.
    /// </summary>
    public string GeneratedCredentialNodeName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a user-facing page error message.
    /// </summary>
    public string ErrorMessage { get; private set; } = string.Empty;

    /// <summary>
    /// Handles the initial page request.
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        await LoadAsync(cancellationToken);
    }

    /// <summary>
    /// Creates or updates the basic data of a design node.
    /// </summary>
    public async Task<IActionResult> OnPostUpsertAsync(CancellationToken cancellationToken = default)
    {
        RemoveModelStateBranch(nameof(Policy));
        RemoveModelStateBranch(nameof(CredentialPackage));
        RemoveModelStateField(nameof(DesignNodeInput.DesignNodeId));
        RemoveCredentialPackageModelState();

        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        await UpsertBasicData(cancellationToken);
        return RedirectToPage();
    }

    /// <summary>
    /// Updates the lifecycle status of one design node.
    /// </summary>
    public async Task<IActionResult> OnPostSetStatusAsync(
        string designNodeId,
        RuntimeDesignNodeStatus status,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await SetStatus(designNodeId, status, cancellationToken);
            SetMessage("Design node updated", $"Design node moved to {status}.", "success");
        }
        catch (Exception ex)
        {
            SetMessage("Design node update failed", ex.Message, "error");
        }

        return RedirectToPage();
    }

    /// <summary>
    /// Validates Runtime outbound connectivity to Design.
    /// </summary>
    public async Task<IActionResult> OnPostValidateConnectionAsync(string designNodeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _connectionService.ValidateConnectionAsync(designNodeId, cancellationToken);
            SetMessage(
                result.Succeeded ? "Connection check succeeded" : "Connection check failed",
                result.Message,
                result.Succeeded ? "success" : "error");
        }
        catch (Exception ex)
        {
            SetMessage("Connection check failed", ex.Message, "error");
        }

        return RedirectToPage();
    }

    /// <summary>
    /// Checks pending releases for a design node.
    /// </summary>
    public async Task<IActionResult> OnPostCheckPendingAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        SourceKey = sourceKey ?? string.Empty;
        await LoadAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(ErrorMessage))
        {
            SetMessage("Pending releases refreshed", $"{PendingArtifacts.Count} pending release(s) found.", "success");
        }
        else
        {
            SetMessage("Pending releases failed", ErrorMessage, "error");
        }

        return Page();
    }

    /// <summary>
    /// Pulls and applies one pending release.
    /// </summary>
    public async Task<IActionResult> OnPostApplyReleaseAsync(
        string sourceKey,
        string releaseTargetId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceKey) || string.IsNullOrWhiteSpace(releaseTargetId))
        {
            SetMessage("Release apply failed", "Select a pending release to apply.", "error");
            SourceKey = sourceKey ?? string.Empty;
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            var result = await _pullService.ApplyAsync(sourceKey, releaseTargetId, cancellationToken);
            SetMessage(
                result.Accepted ? "Release applied" : "Release rejected",
                result.Message,
                result.Accepted ? "success" : "error");
        }
        catch (Exception ex)
        {
            SetMessage("Release apply failed", ex.Message, "error");
        }

        return RedirectToPage(new { sourceKey });
    }

    /// <summary>
    /// Creates or updates the basic data of a design node from the setup wizard.
    /// </summary>
    public async Task<IActionResult> OnPostWizardUpsertAsync(CancellationToken cancellationToken = default)
    {
        RemoveModelStateBranch(nameof(Policy));
        RemoveModelStateBranch(nameof(CredentialPackage));
        RemoveModelStateField(nameof(DesignNodeInput.DesignNodeId));
        RemoveCredentialPackageModelState();

        if (!ModelState.IsValid)
        {
            return BadRequest(new { message = BuildModelStateMessage() });
        }

        try
        {
            var node = await UpsertBasicData(cancellationToken);
            return new JsonResult(new
            {
                message = "Basic data saved.",
                node = ToClientNode(node)
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Saves the selected distribution policy from the setup wizard.
    /// </summary>
    public async Task<IActionResult> OnPostWizardPolicyAsync(CancellationToken cancellationToken = default)
    {
        RemoveInputModelState();
        RemoveModelStateBranch(nameof(CredentialPackage));
        RemoveCredentialPackageModelState();

        if (!ModelState.IsValid)
        {
            return BadRequest(new { message = BuildModelStateMessage() });
        }

        try
        {
            var node = await GetDesignNode(Policy.DesignNodeId, cancellationToken);
            node.DistributionMode = Policy.DistributionMode;
            node.UpdatedOnUtc = DateTime.UtcNow;
            if (node.Status == RuntimeDesignNodeStatus.Enabled && !IsConfigured(node))
            {
                node.Status = RuntimeDesignNodeStatus.Pending;
                node.IsEnabled = false;
            }

            await _repository.UpsertAsync(node, cancellationToken);
            return new JsonResult(new
            {
                message = "Distribution policy saved.",
                node = ToClientNode(await GetDesignNode(Policy.DesignNodeId, cancellationToken))
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Generates Runtime credentials that Design can import from the setup wizard.
    /// </summary>
    public async Task<IActionResult> OnPostWizardGenerateCredentialsAsync(string designNodeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(designNodeId))
        {
            return BadRequest(new { message = "Select a design node." });
        }

        try
        {
            var credentials = await _connectionService.GenerateCredentialPackageAsync(
                designNodeId,
                ResolveCurrentBaseUrl(),
                cancellationToken);

            return new JsonResult(new
            {
                message = "Runtime credentials generated.",
                credentialsJson = credentials.Json,
                node = ToClientNode(await GetDesignNode(designNodeId, cancellationToken))
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Imports Design credentials into Runtime from the setup wizard.
    /// </summary>
    public async Task<IActionResult> OnPostWizardImportCredentialsAsync(CancellationToken cancellationToken = default)
    {
        RemoveInputModelState();
        RemovePolicyModelState();

        if (string.IsNullOrWhiteSpace(CredentialPackage.DesignNodeId) ||
            string.IsNullOrWhiteSpace(CredentialPackage.CredentialsJson))
        {
            return BadRequest(new { message = "Select a design node and provide the Design credentials JSON." });
        }

        try
        {
            await _connectionService.ImportCredentialPackageAsync(new ImportRuntimeDesignNodeCredentialPackageInput
            {
                DesignNodeId = CredentialPackage.DesignNodeId,
                Package = CredentialPackage.CredentialsJson
            }, cancellationToken);

            return new JsonResult(new
            {
                message = "Design credentials imported.",
                node = ToClientNode(await GetDesignNode(CredentialPackage.DesignNodeId, cancellationToken))
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Validates Runtime outbound connectivity to Design from the setup wizard.
    /// </summary>
    public async Task<IActionResult> OnPostWizardValidateConnectionAsync(string designNodeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(designNodeId))
        {
            return BadRequest(new { message = "Select a design node." });
        }

        try
        {
            var result = await _connectionService.ValidateConnectionAsync(designNodeId, cancellationToken);
            return new JsonResult(new
            {
                message = result.Message,
                succeeded = result.Succeeded,
                node = ToClientNode(await GetDesignNode(designNodeId, cancellationToken))
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Updates the lifecycle status of one design node from the setup wizard.
    /// </summary>
    public async Task<IActionResult> OnPostWizardSetStatusAsync(
        string designNodeId,
        RuntimeDesignNodeStatus status,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(designNodeId))
        {
            return BadRequest(new { message = "Select a design node." });
        }

        try
        {
            await SetStatus(designNodeId, status, cancellationToken);
            return new JsonResult(new
            {
                message = $"Design node moved to {status}.",
                node = ToClientNode(await GetDesignNode(designNodeId, cancellationToken))
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<RuntimeDesignNode> UpsertBasicData(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        RuntimeDesignNode current = null;
        Id id;

        if (string.IsNullOrWhiteSpace(Input.DesignNodeId))
        {
            id = Id.New();
        }
        else if (TryParseId(Input.DesignNodeId, out var parsedId))
        {
            id = parsedId;
            current = await _repository.GetByIdAsync(id, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("Design node id is invalid.");
        }

        var node = new RuntimeDesignNode
        {
            Id = current?.Id ?? id,
            Key = Input.Key.Trim(),
            Name = Input.Name.Trim(),
            EndpointBaseUri = current?.EndpointBaseUri ?? string.Empty,
            RemoteRuntimeNodeId = current?.RemoteRuntimeNodeId ?? string.Empty,
            DistributionMode = current?.DistributionMode ?? DistributionConnectionMode.HybridSync,
            AccessTokenTtlSeconds = current?.AccessTokenTtlSeconds > 0 ? current.AccessTokenTtlSeconds : 86_400,
            TokenRefreshSkewSeconds = current?.TokenRefreshSkewSeconds > 0 ? current.TokenRefreshSkewSeconds : 300,
            TokenValidationCacheTtlSeconds = current?.TokenValidationCacheTtlSeconds > 0 ? current.TokenValidationCacheTtlSeconds : 300,
            InboundClientId = current?.InboundClientId ?? string.Empty,
            InboundKeyId = current?.InboundKeyId ?? string.Empty,
            InboundSecretHash = current?.InboundSecretHash ?? string.Empty,
            InboundAllowedScopes = current?.InboundAllowedScopes ?? string.Empty,
            InboundCredentialStatus = current?.InboundCredentialStatus ?? ConnectionCredentialStatus.Missing,
            InboundCredentialCreatedAtUtc = current?.InboundCredentialCreatedAtUtc,
            InboundCredentialRotatedAtUtc = current?.InboundCredentialRotatedAtUtc,
            InboundCredentialRevokedAtUtc = current?.InboundCredentialRevokedAtUtc,
            InboundLastTokenIssuedAtUtc = current?.InboundLastTokenIssuedAtUtc,
            InboundLastTokenFailedAtUtc = current?.InboundLastTokenFailedAtUtc,
            InboundLastFailureReason = current?.InboundLastFailureReason ?? string.Empty,
            OutboundClientId = current?.OutboundClientId ?? string.Empty,
            OutboundKeyId = current?.OutboundKeyId ?? string.Empty,
            ProtectedOutboundSecret = current?.ProtectedOutboundSecret ?? string.Empty,
            OutboundRequestedScopes = current?.OutboundRequestedScopes ?? string.Empty,
            OutboundCredentialStatus = current?.OutboundCredentialStatus ?? ConnectionCredentialStatus.Missing,
            OutboundCredentialImportedAtUtc = current?.OutboundCredentialImportedAtUtc,
            OutboundLastTokenReceivedAtUtc = current?.OutboundLastTokenReceivedAtUtc,
            Description = Input.Description?.Trim() ?? string.Empty,
            Status = current?.Status ?? RuntimeDesignNodeStatus.Pending,
            IsEnabled = current?.Status == RuntimeDesignNodeStatus.Enabled,
            CreatedOnUtc = current?.CreatedOnUtc == default ? now : current?.CreatedOnUtc ?? now,
            UpdatedOnUtc = now
        };

        if (node.Status == RuntimeDesignNodeStatus.Enabled && !IsConfigured(node))
        {
            node.Status = RuntimeDesignNodeStatus.Pending;
            node.IsEnabled = false;
        }

        await _repository.UpsertAsync(node, cancellationToken);
        return await GetDesignNode(node.Id.ToString(), cancellationToken);
    }

    private async Task SetStatus(
        string designNodeId,
        RuntimeDesignNodeStatus status,
        CancellationToken cancellationToken)
    {
        var node = await GetDesignNode(designNodeId, cancellationToken);
        if (!CanTransition(node.Status, status))
        {
            throw new InvalidOperationException($"Design node cannot transition from {node.Status} to {status}.");
        }

        if (status == RuntimeDesignNodeStatus.Enabled && !IsConfigured(node))
        {
            throw new InvalidOperationException("Complete the required credentials before enabling the design node.");
        }

        await _repository.SetStatusAsync(node.Id, status, cancellationToken);
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        DesignNodes = (await _repository.GetAllAsync(cancellationToken))
            .OrderByDescending(x => x.UpdatedOnUtc)
            .ThenBy(x => x.Name)
            .ToArray();

        if (string.IsNullOrWhiteSpace(SourceKey))
        {
            return;
        }

        try
        {
            PendingArtifacts = (await _pullService.GetPendingAsync(SourceKey, cancellationToken))
                .Select(x => new PendingArtifactViewModel
                {
                    ReleaseTargetId = x.ReleaseTargetId,
                    OrchestrationDefinitionKey = x.OrchestrationDefinitionKey,
                    Version = x.Version,
                    Checksum = x.Checksum,
                    CorrelationId = x.CorrelationId,
                    PromotedBy = x.PromotedBy,
                    PromotedOnUtc = x.PromotedOnUtc
                })
                .OrderByDescending(x => x.PromotedOnUtc)
                .ToArray();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task<RuntimeDesignNode> GetDesignNode(string designNodeId, CancellationToken cancellationToken)
    {
        if (!TryParseId(designNodeId, out var id))
        {
            throw new InvalidOperationException("Design node id is invalid.");
        }

        return await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Design node was not found.");
    }

    private string ResolveCurrentBaseUrl()
        => $"{Request.Scheme}://{Request.Host}".TrimEnd('/');

    private void RemoveModelStateBranch(string prefix)
    {
        var keys = ModelState.Keys
            .Where(x => x.Equals(prefix, StringComparison.Ordinal) ||
                        x.StartsWith($"{prefix}.", StringComparison.Ordinal))
            .ToArray();

        foreach (var key in keys)
        {
            ModelState.Remove(key);
        }
    }

    private void RemoveModelStateField(string fieldName)
    {
        var keys = ModelState.Keys
            .Where(x => x.Equals(fieldName, StringComparison.Ordinal) ||
                        x.EndsWith($".{fieldName}", StringComparison.Ordinal))
            .ToArray();

        foreach (var key in keys)
        {
            ModelState.Remove(key);
        }
    }

    private void RemoveInputModelState()
    {
        RemoveModelStateBranch(nameof(Input));
        RemoveModelStateField(nameof(DesignNodeInput.DesignNodeId));
        RemoveModelStateField(nameof(DesignNodeInput.Key));
        RemoveModelStateField(nameof(DesignNodeInput.Name));
        RemoveModelStateField(nameof(DesignNodeInput.Description));
    }

    private void RemovePolicyModelState()
    {
        RemoveModelStateBranch(nameof(Policy));
        RemoveModelStateField(nameof(DesignNodePolicyInput.DesignNodeId));
        RemoveModelStateField(nameof(DesignNodePolicyInput.DistributionMode));
    }

    private void RemoveCredentialPackageModelState()
    {
        RemoveModelStateField(nameof(RuntimeDesignNodeCredentialPackageInput.DesignNodeId));
        RemoveModelStateField(nameof(RuntimeDesignNodeCredentialPackageInput.CredentialsJson));
    }

    private void SetMessage(string title, string body, string type)
    {
        TempData["OrchestratorMessage.Title"] = title;
        TempData["OrchestratorMessage.Body"] = body;
        TempData["OrchestratorMessage.Type"] = type;
    }

    private string BuildModelStateMessage()
    {
        var errors = ModelState.Values
            .SelectMany(x => x.Errors)
            .Select(x => x.ErrorMessage)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        return errors.Length == 0
            ? "Review the design node capture."
            : string.Join(Environment.NewLine, errors);
    }

    private static bool CanTransition(RuntimeDesignNodeStatus current, RuntimeDesignNodeStatus next)
        => (current, next) switch
        {
            (RuntimeDesignNodeStatus.Pending, RuntimeDesignNodeStatus.Suspend) => true,
            (RuntimeDesignNodeStatus.Pending, RuntimeDesignNodeStatus.Enabled) => true,
            (RuntimeDesignNodeStatus.Suspend, RuntimeDesignNodeStatus.Pending) => true,
            (RuntimeDesignNodeStatus.Suspend, RuntimeDesignNodeStatus.Enabled) => true,
            (RuntimeDesignNodeStatus.Enabled, RuntimeDesignNodeStatus.Suspend) => true,
            _ when current == next => true,
            _ => false
        };

    private static bool IsConfigured(RuntimeDesignNode node)
    {
        var needsRuntimeCredentials = NeedsRuntimeCredentials(node.DistributionMode);
        var needsDesignCredentials = NeedsDesignCredentials(node.DistributionMode);
        var hasRuntimeCredentials = node.InboundCredentialStatus == ConnectionCredentialStatus.Active;
        var hasDesignCredentials = node.OutboundCredentialStatus == ConnectionCredentialStatus.Active;
        var hasDesignEndpoint = !string.IsNullOrWhiteSpace(node.EndpointBaseUri);
        var hasRemoteRuntimeNodeId = !string.IsNullOrWhiteSpace(node.RemoteRuntimeNodeId);

        return (!needsRuntimeCredentials || hasRuntimeCredentials) &&
            (!needsDesignCredentials || (hasDesignCredentials && hasDesignEndpoint && hasRemoteRuntimeNodeId));
    }

    private static bool NeedsRuntimeCredentials(DistributionConnectionMode mode)
        => mode is DistributionConnectionMode.DesignPublishesToRuntime or DistributionConnectionMode.HybridSync;

    private static bool NeedsDesignCredentials(DistributionConnectionMode mode)
        => mode is DistributionConnectionMode.RuntimeFetchesFromDesign or DistributionConnectionMode.HybridSync;

    private static bool CanCheckConnection(RuntimeDesignNode node)
        => node.Status == RuntimeDesignNodeStatus.Enabled &&
           node.DistributionMode is DistributionConnectionMode.RuntimeFetchesFromDesign or DistributionConnectionMode.HybridSync;

    private static bool TryParseId(string value, out Id id)
    {
        id = default;
        if (!Ulid.TryParse(value, out var parsed))
        {
            return false;
        }

        id = new Id(parsed);
        return true;
    }

    private static object ToClientNode(RuntimeDesignNode node)
        => new
        {
            id = node.Id.ToString(),
            key = node.Key,
            name = node.Name,
            endpointBaseUri = node.EndpointBaseUri,
            remoteRuntimeNodeId = node.RemoteRuntimeNodeId,
            distributionMode = node.DistributionMode.ToString(),
            distributionModeLabel = DistributionModeLabel(node.DistributionMode.ToString()),
            distributionModeDescription = DistributionModeDescription(node.DistributionMode.ToString()),
            status = node.Status.ToString(),
            statusLabel = ReadableLabel(node.Status.ToString()),
            inboundCredentialStatus = node.InboundCredentialStatus.ToString(),
            inboundCredentialStatusLabel = ReadableLabel(node.InboundCredentialStatus.ToString()),
            inboundClientId = node.InboundClientId,
            inboundKeyId = node.InboundKeyId,
            inboundAllowedScopes = node.InboundAllowedScopes,
            inboundCredentialCreatedAt = DateOrDash(node.InboundCredentialCreatedAtUtc),
            inboundCredentialRotatedAt = DateOrDash(node.InboundCredentialRotatedAtUtc),
            inboundCredentialRevokedAt = DateOrDash(node.InboundCredentialRevokedAtUtc),
            inboundLastTokenIssuedAt = DateOrDash(node.InboundLastTokenIssuedAtUtc),
            inboundLastTokenFailedAt = DateOrDash(node.InboundLastTokenFailedAtUtc),
            inboundLastFailureReason = node.InboundLastFailureReason,
            outboundCredentialStatus = node.OutboundCredentialStatus.ToString(),
            outboundCredentialStatusLabel = ReadableLabel(node.OutboundCredentialStatus.ToString()),
            outboundClientId = node.OutboundClientId,
            outboundKeyId = node.OutboundKeyId,
            outboundRequestedScopes = node.OutboundRequestedScopes,
            outboundCredentialImportedAt = DateOrDash(node.OutboundCredentialImportedAtUtc),
            outboundLastTokenReceivedAt = DateOrDash(node.OutboundLastTokenReceivedAtUtc),
            accessTokenTtlSeconds = node.AccessTokenTtlSeconds.ToString(),
            tokenRefreshSkewSeconds = node.TokenRefreshSkewSeconds.ToString(),
            tokenValidationCacheTtlSeconds = node.TokenValidationCacheTtlSeconds.ToString(),
            description = node.Description,
            isEnabled = node.IsEnabled.ToString(),
            createdOnUtc = DateOrDash(node.CreatedOnUtc),
            updatedOnUtc = DateOrDash(node.UpdatedOnUtc),
            isConfigured = IsConfigured(node).ToString(),
            canCheckConnection = CanCheckConnection(node).ToString()
        };

    private static string DateOrDash(DateTime? value)
        => value.HasValue ? value.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-";

    private static string DateOrDash(DateTime value)
        => value == default ? "-" : value.ToString("yyyy-MM-dd HH:mm:ss");

    private static string DistributionModeLabel(string mode)
        => mode switch
        {
            "DesignPublishesToRuntime" => "Design pushes to Runtime",
            "RuntimeFetchesFromDesign" => "Runtime pulls from Design",
            "HybridSync" => "Hybrid",
            _ => TextOrDash(mode)
        };

    private static string DistributionModeDescription(string mode)
        => mode switch
        {
            "DesignPublishesToRuntime" => "Design publishes approved releases into this Runtime node.",
            "RuntimeFetchesFromDesign" => "Runtime owners pull approved releases manually from Design.",
            "HybridSync" => "Design can push and Runtime can pull releases.",
            _ => string.Empty
        };

    private static string ReadableLabel(string value)
        => value switch
        {
            "RuntimeFetchesFromDesign" => "Runtime pulls from Design",
            "DesignPublishesToRuntime" => "Design pushes to Runtime",
            "HybridSync" => "Hybrid",
            _ => TextOrDash(value)
        };

    private static string TextOrDash(string value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value;
}
