using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
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
    private readonly IRuntimeDesignNodeSecretProtector _secretProtector;
    private readonly IControlPlaneArtifactPullService _pullService;
    private readonly IRuntimeDesignNodeConnectionService _connectionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    public IndexModel(
        IRuntimeDesignNodeRepository repository,
        IRuntimeDesignNodeSecretProtector secretProtector,
        IControlPlaneArtifactPullService pullService,
        IRuntimeDesignNodeConnectionService connectionService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _secretProtector = secretProtector ?? throw new ArgumentNullException(nameof(secretProtector));
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
    /// Gets or sets the credential package import input.
    /// </summary>
    [BindProperty]
    public RuntimeDesignNodeCredentialPackageInput CredentialPackage { get; set; } = new();

    /// <summary>
    /// Gets a generated credential package JSON value.
    /// </summary>
    public string GeneratedCredentialJson { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a generated credential package Base64 value.
    /// </summary>
    public string GeneratedCredentialBase64 { get; private set; } = string.Empty;

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
    /// Creates or updates a design node.
    /// </summary>
    public async Task<IActionResult> OnPostUpsertAsync(CancellationToken cancellationToken = default)
    {
        if (!ValidateSecretInput())
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        var now = DateTime.UtcNow;
        var id = TryParseId(Input.DesignNodeId, out var parsedId) ? parsedId : Id.New();
        var current = await _repository.GetByIdAsync(id, cancellationToken);
        var key = Input.Key.Trim();
        var protectedSecret = string.IsNullOrWhiteSpace(Input.Secret)
            ? current?.ProtectedOutboundSecret ?? string.Empty
            : _secretProtector.Protect(Input.Secret.Trim());
        var designNode = new RuntimeDesignNode
        {
            Id = current?.Id ?? id,
            Key = key,
            Name = Input.Name.Trim(),
            EndpointBaseUri = Input.EndpointBaseUri.Trim().TrimEnd('/'),
            RemoteRuntimeNodeId = Input.RemoteRuntimeNodeId.Trim(),
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
            OutboundClientId = Input.ClientId.Trim(),
            OutboundKeyId = Input.KeyId?.Trim() ?? current?.OutboundKeyId ?? string.Empty,
            ProtectedOutboundSecret = protectedSecret,
            OutboundRequestedScopes = current?.OutboundRequestedScopes ?? "release:read artifact:read artifact:ack connection:validate",
            OutboundCredentialStatus = string.IsNullOrWhiteSpace(Input.ClientId) || string.IsNullOrWhiteSpace(protectedSecret)
                ? ConnectionCredentialStatus.Missing
                : ConnectionCredentialStatus.Active,
            OutboundCredentialImportedAtUtc = string.IsNullOrWhiteSpace(Input.Secret)
                ? current?.OutboundCredentialImportedAtUtc
                : now,
            OutboundLastTokenReceivedAtUtc = current?.OutboundLastTokenReceivedAtUtc,
            Description = Input.Description?.Trim() ?? string.Empty,
            IsEnabled = Input.IsEnabled,
            CreatedOnUtc = current?.CreatedOnUtc == default ? now : current?.CreatedOnUtc ?? now,
            UpdatedOnUtc = now
        };

        await _repository.UpsertAsync(designNode, cancellationToken);
        SetMessage("Design node saved", $"Design node '{designNode.Key}' is ready for runtime distribution.", "success");
        return designNode.IsEnabled
            ? RedirectToPage(new { sourceKey = designNode.Key })
            : RedirectToPage();
    }

    /// <summary>
    /// Enables or disables one design node.
    /// </summary>
    public async Task<IActionResult> OnPostSetEnabledAsync(string designNodeId, bool isEnabled, CancellationToken cancellationToken = default)
    {
        if (TryParseId(designNodeId, out var id))
        {
            await _repository.SetEnabledAsync(id, isEnabled, cancellationToken);
            SetMessage(isEnabled ? "Design node enabled" : "Design node disabled", "Runtime distribution sources were updated.", "success");
        }

        return RedirectToPage();
    }

    /// <summary>
    /// Generates Runtime credentials that Design can import.
    /// </summary>
    public async Task<IActionResult> OnPostGenerateCredentialsAsync(string designNodeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var package = await _connectionService.GenerateCredentialPackageAsync(
                designNodeId,
                ResolveCurrentBaseUrl(),
                cancellationToken);
            GeneratedCredentialJson = package.Json;
            GeneratedCredentialBase64 = package.Base64;
            SetMessage("Credential package generated", "Copy the package now. The secret is shown only in this response.", "success");
        }
        catch (Exception ex)
        {
            SetMessage("Credential package failed", ex.Message, "error");
        }

        await LoadAsync(cancellationToken);
        return Page();
    }

    /// <summary>
    /// Imports Design credentials for Runtime outbound calls.
    /// </summary>
    public async Task<IActionResult> OnPostImportCredentialsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CredentialPackage.DesignNodeId) ||
            string.IsNullOrWhiteSpace(CredentialPackage.Package))
        {
            SetMessage("Credential import failed", "Select a design node and provide the JSON or Base64 package.", "error");
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            await _connectionService.ImportCredentialPackageAsync(new ImportRuntimeDesignNodeCredentialPackageInput
            {
                DesignNodeId = CredentialPackage.DesignNodeId,
                Package = CredentialPackage.Package
            }, cancellationToken);
            SetMessage("Credential imported", "Runtime can now request short-lived tokens from the design node.", "success");
        }
        catch (Exception ex)
        {
            SetMessage("Credential import failed", ex.Message, "error");
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
                result.Succeeded ? "Connection validated" : "Connection validation failed",
                result.Message,
                result.Succeeded ? "success" : "error");
        }
        catch (Exception ex)
        {
            SetMessage("Connection validation failed", ex.Message, "error");
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
            ModelState.AddModelError(string.Empty, "Select a pending release to apply.");
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

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        DesignNodes = await _repository.GetAllAsync(cancellationToken);

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
                    EnvironmentKey = x.EnvironmentKey,
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

    private bool ValidateSecretInput()
    {
        if (!string.IsNullOrWhiteSpace(Input.DesignNodeId) && !TryParseId(Input.DesignNodeId, out _))
        {
            ModelState.AddModelError(nameof(Input.DesignNodeId), "Design node id is invalid.");
        }

        return ModelState.IsValid;
    }

    private void SetMessage(string title, string body, string type)
    {
        TempData["OrchestratorMessage.Title"] = title;
        TempData["OrchestratorMessage.Body"] = body;
        TempData["OrchestratorMessage.Type"] = type;
    }

    private string ResolveCurrentBaseUrl()
        => $"{Request.Scheme}://{Request.Host}".TrimEnd('/');

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

}
