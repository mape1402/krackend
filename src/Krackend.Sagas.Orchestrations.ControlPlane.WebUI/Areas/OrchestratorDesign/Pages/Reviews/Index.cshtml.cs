using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.Reviews;

/// <summary>Lists lifecycle reviews independently from orchestration roadmaps.</summary>
public sealed class IndexModel : PageModel
{
    private readonly IOrchestrationVersionReviewApplicationService _reviewService;
    private readonly IOrchestrationVersionApplicationService _versionService;
    private readonly IOrchestrationApplicationService _orchestrationService;

    /// <summary>Initializes a new instance.</summary>
    public IndexModel(
        IOrchestrationVersionReviewApplicationService reviewService,
        IOrchestrationVersionApplicationService versionService,
        IOrchestrationApplicationService orchestrationService)
    {
        _reviewService = reviewService ?? throw new ArgumentNullException(nameof(reviewService));
        _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
        _orchestrationService = orchestrationService ?? throw new ArgumentNullException(nameof(orchestrationService));
    }

    /// <summary>Displays the review catalog; records are loaded on demand.</summary>
    public void OnGet() { }

    /// <summary>Loads a review and its orchestration for the review dialog.</summary>
    public async Task<IActionResult> OnGetDetailAsync(string reviewId, CancellationToken cancellationToken = default)
    {
        try
        {
            var review = await _reviewService.GetById(new GetOrchestrationVersionReviewByIdQuery(reviewId ?? string.Empty), cancellationToken);
            if (review is null) return NotFound();
            var version = await _versionService.GetById(new GetOrchestrationVersionByIdQuery(review.OrchestrationVersionId), cancellationToken);
            if (version is null) return NotFound();
            var orchestration = await _orchestrationService.GetById(new GetOrchestrationDefinitionByIdQuery(version.OrchestrationDefinitionId), cancellationToken);
            var pending = review.ApplicationStatus == OrchestrationVersionReviewApplicationStatus.Pending;
            return new JsonResult(new
            {
                review.Id, orchestrationName = orchestration?.Name ?? "Orchestration unavailable", orchestrationKey = orchestration?.Key ?? string.Empty,
                version = string.IsNullOrWhiteSpace(version.Version) ? "Draft roadmap" : version.Version,
                versionStatus = version.Status.ToString(), operation = review.Operation.ToString(),
                approval = review.ApprovalStatus.ToString(), application = review.ApplicationStatus.ToString(),
                review.RequestedBy, requestedOn = review.RequestedOnUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                review.RejectionComment, review.AbortReason, review.DeployedArtifactId,
                canApprove = pending && review.ApprovalStatus == OrchestrationVersionReviewApprovalStatus.Pending,
                canApply = pending && review.ApprovalStatus == OrchestrationVersionReviewApprovalStatus.Approved,
                canAbort = pending && review.ApprovalStatus != OrchestrationVersionReviewApprovalStatus.Rejected,
                roadmapUrl = Url.Page("/OrchestrationVersions/Details", new { area = "OrchestratorDesign", orchestrationId = version.OrchestrationDefinitionId, versionId = review.OrchestrationVersionId }),
                detailsUrl = Url.Page("/Reviews/Details", new { area = "OrchestratorDesign", reviewId = review.Id })
            });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return StatusCode(500, "The lifecycle review could not be loaded.");
        }
    }

    /// <summary>Returns one storage-filtered page for the review catalog.</summary>
    public async Task<IActionResult> OnGetListAsync([FromQuery(Name = "page")] int page = 1, int pageSize = 25,
        string search = null, string sort = null, bool descending = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _reviewService.GetAll(new GetAllOrchestrationVersionReviewsQuery(new ApplicationPagedSettings
            {
                PageNumber = Math.Max(1, page), PageSize = Math.Clamp(pageSize, 1, 100)
            }) { SearchText = search?.Trim(), SortField = sort, Descending = descending }, cancellationToken);
            var items = new List<object>();
            foreach (var review in result.Rows)
            {
                OrchestrationVersionModel version = null;
                OrchestrationDefinitionModel orchestration = null;
                try
                {
                    version = await _versionService.GetById(new GetOrchestrationVersionByIdQuery(review.OrchestrationVersionId), cancellationToken);
                    orchestration = await _orchestrationService.GetById(new GetOrchestrationDefinitionByIdQuery(version.OrchestrationDefinitionId), cancellationToken);
                }
                catch (KeyNotFoundException) { }
                var name = orchestration?.Name ?? "Orchestration unavailable";
                var versionLabel = string.IsNullOrWhiteSpace(version?.Version) ? "Draft roadmap" : $"Version {version.Version}";
                items.Add(new
                {
                    id = review.Id, displayName = name, key = orchestration?.Key ?? string.Empty,
                    operation = review.Operation.ToString(), orchestration = name, version = versionLabel,
                    approval = review.ApprovalStatus.ToString(), application = review.ApplicationStatus.ToString(),
                    requested = review.RequestedOnUtc.ToString("yyyy-MM-dd HH:mm"), requestedBy = review.RequestedBy,
                    description = $"{review.Operation} | {versionLabel}",
                    openUrl = Url.Page("/Reviews/Details", new { area = "OrchestratorDesign", reviewId = review.Id })
                });
            }
            return new JsonResult(new { items, totalCount = result.TotalRows,
                hasMore = (long)result.PageNumber * result.PageSize < result.TotalRows,
                search = search?.Trim() ?? string.Empty, sort = sort ?? string.Empty, descending });
        }
        catch (NotSupportedException)
        {
            return StatusCode(503, "The configured review storage does not support catalog search.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return StatusCode(500, "Lifecycle reviews could not be loaded. Try again in a moment.");
        }
    }
}
