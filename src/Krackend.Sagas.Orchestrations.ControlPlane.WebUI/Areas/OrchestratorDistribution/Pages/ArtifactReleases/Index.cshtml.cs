using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Artifacts;

public sealed class IndexModel : PageModel
{
    private const string NewVersionArtifactType = "orchestration-version.new";
    private const string DeprecatedArtifactType = "orchestration-version.deprecated";
    private const string ArchivedArtifactType = "orchestration-version.archived";
    private const string LegacySnapshotArtifactType = "orchestration-version-snapshot";
    private readonly IArtifactApplicationService _service;

    public IndexModel(
        IArtifactApplicationService service,
        IOrchestrationNodePolicyApplicationService policyService)
    {
        _service = service;
    }

    [BindProperty(SupportsGet = true)] public string OrchestrationId { get; set; } = string.Empty;

    /// <summary>Returns a concise presentation using the shared Zoorce badge tones.</summary>
    public ArtifactTypePresentation GetArtifactTypePresentation(string artifactType)
        => artifactType switch
        {
            NewVersionArtifactType => new ArtifactTypePresentation("New version", "success"),
            DeprecatedArtifactType => new ArtifactTypePresentation("Deprecated", "warning"),
            ArchivedArtifactType => new ArtifactTypePresentation("Archived", "danger"),
            LegacySnapshotArtifactType => new ArtifactTypePresentation("Snapshot", ""),
            _ => new ArtifactTypePresentation(string.IsNullOrWhiteSpace(artifactType) ? "Unknown" : artifactType, ""),
        };

    public Task OnGetAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task<IActionResult> OnGetListAsync([FromQuery(Name = "page")] int page = 1, int pageSize = 25,
        string search = null, string sort = null, bool descending = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.Search(new ApplicationPagedSettings { PageNumber = Math.Max(1, page), PageSize = Math.Clamp(pageSize, 1, 100) },
                OrchestrationId, search?.Trim(), sort, descending, cancellationToken);
            return new JsonResult(new
            {
                items = result.Rows.Select(x => new { id = x.Id, key = x.VersionNumber, displayName = x.OrchestrationDisplayName,
                    version = x.VersionNumber, artifactType = x.ArtifactType, typeLabel = GetArtifactTypePresentation(x.ArtifactType).Label,
                    typeTone = GetArtifactTypePresentation(x.ArtifactType).Tone, created = x.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                    description = x.VersionLabel, checksum = x.Checksum,
                    releaseUrl = Url.Page("/Promotions/Index", new { area = "OrchestratorDistribution", artifactId = x.Id }) }),
                totalCount = result.TotalRows, hasMore = (long)result.PageNumber * result.PageSize < result.TotalRows,
                search = search?.Trim() ?? string.Empty, sort = sort ?? string.Empty, descending
            });
        }
        catch (NotSupportedException)
        {
            return StatusCode(503, "The configured artifact storage does not support catalog search.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return StatusCode(500, "Artifacts could not be loaded. Try again in a moment.");
        }
    }

    public async Task<IActionResult> OnGetDetailsAsync(string artifactId, CancellationToken cancellationToken = default)
    {
        if (!Ulid.TryParse(artifactId, out _)) return BadRequest("Artifact id is invalid.");
        try
        {
            var item = await _service.GetById(artifactId, cancellationToken);
            if (item is null) return NotFound("Artifact was not found.");
            return new JsonResult(new { displayName = item.OrchestrationDisplayName, artifactType = item.ArtifactType,
                versionId = item.OrchestrationVersionId, versionNumber = item.VersionNumber, versionLabel = item.VersionLabel,
                createdAt = item.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm"), checksum = item.Checksum, payload = item.Payload });
        }
        catch (NotSupportedException)
        {
            return StatusCode(503, "The configured artifact storage does not support detail lookup.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return StatusCode(500, "Artifact details are unavailable. Try again in a moment.");
        }
    }

    /// <summary>Describes the visible label and semantic Zoorce tone for an artifact type.</summary>
    public sealed record ArtifactTypePresentation(string Label, string Tone);
}
