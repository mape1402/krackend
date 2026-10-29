using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Artifacts;

/// <summary>
/// Displays runtime artifacts and allows operators to request ingress standup again.
/// </summary>
public sealed class IndexModel : PageModel
{
    private readonly IRuntimeArtifactRepository _artifactRepository;
    private readonly IRuntimeArtifactDeploymentHistoryRepository _deploymentHistoryRepository;
    private readonly IRuntimeArtifactReadyNotifier _artifactReadyNotifier;
    private readonly IRuntimeArtifactTransferService _artifactTransferService;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    public IndexModel(
        IRuntimeArtifactRepository artifactRepository,
        IRuntimeArtifactDeploymentHistoryRepository deploymentHistoryRepository,
        IRuntimeArtifactReadyNotifier artifactReadyNotifier,
        IRuntimeArtifactTransferService artifactTransferService)
    {
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
        _deploymentHistoryRepository = deploymentHistoryRepository ?? throw new ArgumentNullException(nameof(deploymentHistoryRepository));
        _artifactReadyNotifier = artifactReadyNotifier ?? throw new ArgumentNullException(nameof(artifactReadyNotifier));
        _artifactTransferService = artifactTransferService ?? throw new ArgumentNullException(nameof(artifactTransferService));
    }

    /// <summary>Orchestration selected for version details.</summary>
    [BindProperty(SupportsGet = true)] public string OrchestrationKey { get; set; } = string.Empty;

    /// <summary>
    /// Handles the initial page request.
    /// </summary>
    public Task OnGetAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>Exports a runtime artifact as an integrity-protected release candidate.</summary>
    public async Task<IActionResult> OnGetExportCandidateAsync(string artifactId, CancellationToken cancellationToken = default)
    {
        try
        {
            var package = await _artifactTransferService.ExportAsync(artifactId, cancellationToken);
            var json = _artifactTransferService.Serialize(package);
            var filename = $"krackend-runtime-artifact-{package.Delivery.OrchestrationDefinitionKey}-{package.Delivery.Version}.json";
            return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", filename);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            SetMessage("Artifact export failed", exception.Message, "error");
            return RedirectToPage(new { orchestrationKey = OrchestrationKey });
        }
    }

    /// <summary>Imports a portable runtime artifact through the normal deployment pipeline.</summary>
    public async Task<IActionResult> OnPostImportCandidateAsync(IFormFile candidatePackage, CancellationToken cancellationToken = default)
    {
        if (candidatePackage is null || candidatePackage.Length == 0)
        {
            SetMessage("Artifact import failed", "Choose a runtime artifact package first.", "warning");
            return RedirectToPage();
        }

        try
        {
            using var reader = new StreamReader(candidatePackage.OpenReadStream());
            var packageJson = await reader.ReadToEndAsync(cancellationToken);
            var importedBy = User?.Identity?.Name;
            var result = await _artifactTransferService.ImportAsync(
                packageJson,
                string.IsNullOrWhiteSpace(importedBy) ? "runtime-ui" : importedBy,
                cancellationToken);
            SetMessage(
                result.Accepted ? "Artifact candidate applied" : "Artifact candidate rejected",
                result.Message,
                result.Accepted ? "success" : "warning");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or JsonException)
        {
            SetMessage("Artifact import failed", exception.Message, "error");
        }

        return RedirectToPage();
    }

    /// <summary>Inspects a portable runtime package before an operator applies it.</summary>
    public async Task<IActionResult> OnPostInspectCandidateAsync(IFormFile candidatePackage, CancellationToken cancellationToken = default)
    {
        if (candidatePackage is null || candidatePackage.Length == 0)
        {
            return BadRequest(new { message = "Choose a runtime artifact package first." });
        }

        try
        {
            using var reader = new StreamReader(candidatePackage.OpenReadStream());
            var packageJson = await reader.ReadToEndAsync(cancellationToken);
            var inspection = _artifactTransferService.Inspect(packageJson);
            return new JsonResult(new
            {
                sourceArtifactId = inspection.SourceArtifactId,
                orchestrationKey = inspection.OrchestrationKey,
                inspection.Version,
                artifactType = inspection.ArtifactType,
                releaseStrategy = inspection.ReleaseStrategy,
                exportedOnUtc = inspection.ExportedOnUtc.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or JsonException)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    /// <summary>Searches orchestration groups in the configured storage.</summary>
    public async Task<IActionResult> OnGetGroupsAsync([FromQuery(Name = "page")] int page = 1, int pageSize = 25, string search = "", CancellationToken cancellationToken = default)
    {
        if (_artifactRepository is not IRuntimeArtifactUiCatalog catalog) return StatusCode(503);
        var result = await catalog.GetGroups(page, pageSize, search ?? string.Empty, cancellationToken);
        return new JsonResult(new
        {
            items = result.Items.Select(x => new { id = x.Key, key = x.Key, displayName = x.Key, versionCount = x.VersionCount,
                activeCount = x.ActiveCount, latest = x.LatestDeployedOnUtc.ToString("yyyy-MM-dd HH:mm:ss") }),
            totalCount = result.TotalCount, hasMore = (long)result.PageNumber * result.PageSize < result.TotalCount
        });
    }

    /// <summary>Returns a page of versions belonging to one orchestration.</summary>
    public async Task<IActionResult> OnGetVersionsAsync(string orchestrationKey, [FromQuery(Name = "page")] int page = 1, int pageSize = 25, string search = "", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orchestrationKey)) return BadRequest();
        if (_artifactRepository is not IRuntimeArtifactUiCatalog catalog) return StatusCode(503);
        var result = await catalog.GetVersions(orchestrationKey, page, pageSize, search ?? string.Empty, cancellationToken);
        return new JsonResult(new
        {
            items = result.Items.Select(x => new { id = x.Id.ToString(), key = x.Id.ToString(), x.Version, x.Status, x.LifecycleStatus,
                x.LastReleaseId, x.LastControlPlaneArtifactId, x.ReleaseStrategy, x.IsActive, x.IngressGeneration, x.Checksum,
                deployed = x.DeployedOnUtc.ToString("yyyy-MM-dd HH:mm:ss"), x.ActivatedOnUtc, x.ProjectionStartedOnUtc,
                x.ProjectionCompletedOnUtc, x.ProjectionFailedOnUtc, x.ProjectionError }),
            totalCount = result.TotalCount, hasMore = (long)result.PageNumber * result.PageSize < result.TotalCount
        });
    }

    /// <summary>Returns a page of deployment history for an artifact.</summary>
    public async Task<IActionResult> OnGetHistoryAsync(string artifactId, [FromQuery(Name = "page")] int page = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        if (!TryParseId(artifactId, out var id)) return BadRequest();
        if (_deploymentHistoryRepository is not IRuntimeArtifactHistoryCatalog catalog) return StatusCode(503);
        var result = await catalog.GetPage(id, page, pageSize, cancellationToken);
        return new JsonResult(new
        {
            items = result.Items.Select(x => new { x.ArtifactType, x.ReleaseId, x.ReleaseStrategy,
                lifecycleStatus = x.LifecycleStatus.ToString(), runtimeStatus = x.RuntimeStatus.ToString(),
                applied = x.AppliedOnUtc.ToString("yyyy-MM-dd HH:mm:ss") }),
            totalCount = result.TotalCount, hasMore = (long)result.PageNumber * result.PageSize < result.TotalCount
        });
    }

    /// <summary>
    /// Requests local and peer replicas to stand up the selected ready artifact.
    /// </summary>
    public async Task<IActionResult> OnPostRequestStandupAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseId(artifactId, out var parsedArtifactId))
        {
            SetMessage("Invalid artifact", "Artifact id is invalid.", "error");
            return RedirectToPage();
        }

        try
        {
            var artifact = await _artifactRepository.GetById(parsedArtifactId, cancellationToken);
            if (artifact.Status != RuntimeOrchestrationArtifactStatus.Ready || !artifact.IsActive)
            {
                SetMessage("Artifact is not ready", "Only active ready artifacts can request ingress standup.", "warning");
                return RedirectToPage();
            }

            await _artifactReadyNotifier.NotifyReadyAsync(
                RuntimeArtifactReadyGossipMessage.FromArtifact(artifact),
                cancellationToken);
            SetMessage("Standup requested", $"Ingress standup was requested for {artifact.OrchestrationDefinitionKey} v{artifact.Version}.", "success");
        }
        catch (Exception exception)
        {
            SetMessage("Standup request failed", exception.Message, "error");
        }

        return RedirectToPage();
    }

    private void SetMessage(string title, string body, string type)
    {
        TempData["OrchestratorMessage.Title"] = title;
        TempData["OrchestratorMessage.Body"] = body;
        TempData["OrchestratorMessage.Type"] = type;
    }

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
