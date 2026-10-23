using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.Reviews;

/// <summary>Displays and applies an independent orchestration lifecycle review.</summary>
public sealed class DetailsModel : PageModel
{
    private const string Actor = "web-ui";
    private readonly IOrchestrationVersionReviewApplicationService _reviewService;
    private readonly IOrchestrationVersionApplicationService _versionService;
    private readonly IOrchestrationApplicationService _orchestrationService;

    public DetailsModel(
        IOrchestrationVersionReviewApplicationService reviewService,
        IOrchestrationVersionApplicationService versionService,
        IOrchestrationApplicationService orchestrationService)
    {
        _reviewService = reviewService ?? throw new ArgumentNullException(nameof(reviewService));
        _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
        _orchestrationService = orchestrationService ?? throw new ArgumentNullException(nameof(orchestrationService));
    }

    [BindProperty(SupportsGet = true)]
    public string ReviewId { get; set; } = string.Empty;

    public OrchestrationVersionReviewModel Review { get; private set; }

    public OrchestrationVersionModel Version { get; private set; }

    public OrchestrationDefinitionModel Orchestration { get; private set; }

    public string OrchestrationId { get; private set; } = string.Empty;

    public string ErrorMessage { get; private set; } = string.Empty;

    [BindProperty]
    public DeploymentInput ApplyInput { get; set; } = new();

    [BindProperty]
    public string RejectionComment { get; set; } = string.Empty;

    [BindProperty]
    public string AbortReason { get; set; } = string.Empty;

    public bool CanApprove => Review?.ApprovalStatus == OrchestrationVersionReviewApprovalStatus.Pending
        && Review.ApplicationStatus == OrchestrationVersionReviewApplicationStatus.Pending;

    public bool CanApply => Review?.ApprovalStatus == OrchestrationVersionReviewApprovalStatus.Approved
        && Review.ApplicationStatus == OrchestrationVersionReviewApplicationStatus.Pending;

    public bool CanAbort => Review?.ApplicationStatus == OrchestrationVersionReviewApplicationStatus.Pending
        && Review.ApprovalStatus != OrchestrationVersionReviewApprovalStatus.Rejected;

    public async Task<IActionResult> OnGetAsync(string reviewId, CancellationToken cancellationToken = default)
    {
        ReviewId = reviewId ?? string.Empty;
        await LoadAsync(cancellationToken);
        return Review is null || Version is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(string reviewId, CancellationToken cancellationToken = default)
        => await ExecuteAsync(
            reviewId,
            token => _reviewService.Approve(new ApproveOrchestrationVersionReviewCommand(reviewId, Actor), token),
            cancellationToken);

    public async Task<IActionResult> OnPostRejectAsync(string reviewId, CancellationToken cancellationToken = default)
        => await ExecuteAsync(
            reviewId,
            token => _reviewService.Reject(new RejectOrchestrationVersionReviewCommand(reviewId, Actor, RejectionComment ?? string.Empty), token),
            cancellationToken);

    public async Task<IActionResult> OnPostAbortAsync(string reviewId, CancellationToken cancellationToken = default)
        => await ExecuteAsync(
            reviewId,
            token => _reviewService.Abort(new AbortOrchestrationVersionReviewCommand(reviewId, Actor, AbortReason ?? string.Empty), token),
            cancellationToken);

    public async Task<IActionResult> OnPostApplyAsync(string reviewId, CancellationToken cancellationToken = default)
    {
        ModelState.Clear();
        if (!string.IsNullOrWhiteSpace(ApplyInput.Version) &&
            !System.Text.RegularExpressions.Regex.IsMatch(ApplyInput.Version, @"^\d+\.\d+\.\d+$"))
        {
            ModelState.AddModelError(nameof(ApplyInput.Version), "Use semantic version format, for example 1.0.0.");
        }

        await LoadAsync(cancellationToken);
        if (Review?.Operation == OrchestrationVersionReviewOperation.Deploy && string.IsNullOrWhiteSpace(ApplyInput.Version))
        {
            ModelState.AddModelError(nameof(ApplyInput.Version), "A version is required to deploy a draft.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        return await ExecuteAsync(
            reviewId,
            token => _reviewService.Apply(new ApplyOrchestrationVersionReviewCommand(
                reviewId,
                Actor,
                ApplyInput.Version ?? string.Empty,
                ApplyInput.VersionLabel ?? string.Empty,
                ApplyInput.Description ?? string.Empty,
                ApplyInput.Notes ?? string.Empty), token),
            cancellationToken);
    }

    private async Task<IActionResult> ExecuteAsync(
        string reviewId,
        Func<CancellationToken, Task<bool>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            await operation(cancellationToken);
            return RedirectToPage("/Reviews/Details", new { area = "OrchestratorDesign", reviewId });
        }
        catch (Exception exception)
        {
            ReviewId = reviewId;
            ErrorMessage = exception is InvalidOperationException && !string.IsNullOrWhiteSpace(exception.Message)
                ? exception.Message
                : "The lifecycle review could not be updated. Review the data and try again.";
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            Review = await _reviewService.GetById(new GetOrchestrationVersionReviewByIdQuery(ReviewId), cancellationToken);
            if (Review is null)
            {
                return;
            }

            Version = await _versionService.GetById(
                new GetOrchestrationVersionByIdQuery(Review.OrchestrationVersionId),
                cancellationToken);
            OrchestrationId = Version?.OrchestrationDefinitionId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(OrchestrationId))
            {
                Orchestration = await _orchestrationService.GetById(
                    new GetOrchestrationDefinitionByIdQuery(OrchestrationId),
                    cancellationToken);
            }
        }
        catch (Exception exception)
        {
            ErrorMessage = exception is KeyNotFoundException
                ? "The requested lifecycle review no longer exists."
                : "The lifecycle review could not be loaded.";
        }
    }

    public sealed class DeploymentInput
    {
        [MaxLength(64)]
        public string Version { get; set; } = string.Empty;

        [MaxLength(128)]
        public string VersionLabel { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Notes { get; set; } = string.Empty;
    }
}
