using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using DesignPagedSettings = Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.ApplicationPagedSettings;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.Orchestrations;

/// <summary>
/// Represents orchestration details and version management.
/// </summary>
public sealed class DetailsModel : PageModel
{
    private readonly IOrchestrationApplicationService _orchestrationService;
    private readonly IOrchestrationVersionApplicationService _versionService;
    private readonly IOrchestrationVersionReviewApplicationService _reviewService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DetailsModel"/> class.
    /// </summary>
    public DetailsModel(
        IOrchestrationApplicationService orchestrationService,
        IOrchestrationVersionApplicationService versionService,
        IOrchestrationVersionReviewApplicationService reviewService)
    {
        _orchestrationService = orchestrationService ?? throw new ArgumentNullException(nameof(orchestrationService));
        _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
        _reviewService = reviewService ?? throw new ArgumentNullException(nameof(reviewService));
    }

    /// <summary>
    /// Gets or sets the orchestration identifier.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string OrchestrationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets the orchestration data.
    /// </summary>
    public OrchestrationDefinitionModel Orchestration { get; private set; }

    /// <summary>
    /// Gets available versions for this orchestration.
    /// </summary>
    public IReadOnlyCollection<OrchestrationVersionModel> Versions { get; private set; } = Array.Empty<OrchestrationVersionModel>();

    /// <summary>
    /// Gets or sets input for version creation.
    /// </summary>
    [BindProperty]
    public CreateVersionInput NewVersion { get; set; } = new();

    /// <summary>
    /// Gets user-facing error text.
    /// </summary>
    public string ErrorMessage { get; private set; } = string.Empty;

    /// <summary>
    /// Handles GET requests.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(string orchestrationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orchestrationId))
        {
            return RedirectToPage("/Orchestrations/Index", new { area = "OrchestratorDesign" });
        }

        OrchestrationId = orchestrationId;
        await LoadDataAsync(cancellationToken);

        return Orchestration is null ? NotFound() : Page();
    }

    /// <summary>
    /// Handles orchestration active state changes.
    /// </summary>
    public async Task<IActionResult> OnPostToggleActiveAsync(string orchestrationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var orchestration = await _orchestrationService.GetById(new GetOrchestrationDefinitionByIdQuery(orchestrationId), cancellationToken);
            if (orchestration is null)
            {
                return RedirectToPage("/Orchestrations/Index", new { area = "OrchestratorDesign" });
            }

            if (orchestration.IsActive)
            {
                await _orchestrationService.Deactivate(new DeactivateOrchestrationDefinitionCommand(orchestrationId), cancellationToken);
            }
            else
            {
                await _orchestrationService.Activate(new ActivateOrchestrationDefinitionCommand(orchestrationId), cancellationToken);
            }

            return RedirectToPage("/Orchestrations/Details", new { area = "OrchestratorDesign", orchestrationId });
        }
        catch (Exception ex)
        {
            OrchestrationId = orchestrationId;
            ErrorMessage = ToFriendlyErrorMessage(ex);
            await LoadDataAsync(cancellationToken);
            return Page();
        }
    }

    /// <summary>
    /// Handles new version creation.
    /// </summary>
    public async Task<IActionResult> OnPostCreateVersionAsync(string orchestrationId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                OrchestrationId = orchestrationId;
                await LoadDataAsync(cancellationToken);
                return Page();
            }

            if (string.IsNullOrWhiteSpace(NewVersion.CreatedBy))
            {
                NewVersion.CreatedBy = "web-ui";
            }

            var versionId = await _versionService.Create(
                new CreateOrchestrationVersionCommand(
                    orchestrationId,
                    string.Empty,
                    OrchestrationVersionStatus.Draft,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    NewVersion.CreatedBy),
                cancellationToken);

            return RedirectToPage("/OrchestrationVersions/Details", new { area = "OrchestratorDesign", orchestrationId, versionId });
        }
        catch (Exception ex)
        {
            OrchestrationId = orchestrationId;
            ErrorMessage = ToFriendlyErrorMessage(ex);
            await LoadDataAsync(cancellationToken);
            return Page();
        }
    }

    /// <summary>
    /// Handles version status actions from the listing.
    /// </summary>
    public async Task<IActionResult> OnPostVersionActionAsync(string orchestrationId, string versionId, string action, CancellationToken cancellationToken = default)
    {
        const string actor = "web-ui";
        ModelState.Clear();

        try
        {
            var current = await _versionService.GetById(
                new GetOrchestrationVersionByIdQuery(versionId),
                cancellationToken);
            if (current is null || !string.Equals(current.OrchestrationDefinitionId, orchestrationId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The selected orchestration version does not belong to this orchestration.");
            }

            switch (action)
            {
                case "RequestDeployment":
                case "RequestDeprecation":
                case "RequestArchive":
                    var reviewId = await _reviewService.Request(
                        new RequestOrchestrationVersionReviewCommand(versionId, ToReviewOperation(action), actor),
                        cancellationToken);
                    return RedirectToPage("/Reviews/Details", new { area = "OrchestratorDesign", reviewId });
                case "OpenReview":
                    var pendingReview = await _reviewService.GetPending(
                        new GetPendingOrchestrationVersionReviewQuery(versionId), cancellationToken);
                    if (pendingReview is not null)
                    {
                        return RedirectToPage("/Reviews/Details", new { area = "OrchestratorDesign", reviewId = pendingReview.Id });
                    }

                    throw new InvalidOperationException("This version does not have a review awaiting application.");
                case "Abandon":
                    await _versionService.Abandon(new AbandonOrchestrationVersionCommand(versionId, actor), cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException("The requested lifecycle action is not supported.");
            }

            return RedirectToPage("/Orchestrations/Details", new { area = "OrchestratorDesign", orchestrationId });
        }
        catch (Exception ex)
        {
            OrchestrationId = orchestrationId;
            ErrorMessage = ToFriendlyErrorMessage(ex);
            await LoadDataAsync(cancellationToken);
            return Page();
        }
    }

    /// <summary>
    /// Returns allowed version actions by current status.
    /// </summary>
    public IEnumerable<string> GetAllowedActions(OrchestrationVersionStatus status)
    {
        return status switch
        {
            OrchestrationVersionStatus.Draft => new[] { "RequestDeployment", "Abandon" },
            OrchestrationVersionStatus.PendingDeployment or OrchestrationVersionStatus.PendingDeprecation or OrchestrationVersionStatus.PendingArchive => new[] { "OpenReview" },
            OrchestrationVersionStatus.Deployed => new[] { "RequestDeprecation" },
            OrchestrationVersionStatus.Deprecated => new[] { "RequestArchive" },
            _ => Array.Empty<string>()
        };
    }

    private static OrchestrationVersionReviewOperation ToReviewOperation(string action)
        => action switch
        {
            "RequestDeployment" => OrchestrationVersionReviewOperation.Deploy,
            "RequestDeprecation" => OrchestrationVersionReviewOperation.Deprecate,
            "RequestArchive" => OrchestrationVersionReviewOperation.Archive,
            _ => throw new InvalidOperationException("The requested lifecycle action is not supported.")
        };

    private async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            Orchestration = await _orchestrationService.GetById(new GetOrchestrationDefinitionByIdQuery(OrchestrationId), cancellationToken);
            if (Orchestration is null)
            {
                return;
            }

            var versionsResult = await _versionService.GetAll(
                new GetOrchestrationVersionsQuery(
                    OrchestrationId,
                    new DesignPagedSettings
                    {
                        PageNumber = 1,
                        PageSize = 200
                    }),
                cancellationToken);

            Versions = versionsResult.Rows
                .OrderByDescending(x => x.CreatedOnUtc, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception ex)
        {
            ErrorMessage = ToFriendlyErrorMessage(ex);
        }
    }

    private static string ToFriendlyErrorMessage(Exception exception)
        => exception switch
        {
            OrchestrationArtifactDslValidationException dslException =>
                $"The version cannot be published because the DSL configuration at '{dslException.Path}' is not valid. Review that transformation, validation, or condition and try again.",
            InvalidOperationException invalidOperation when !string.IsNullOrWhiteSpace(invalidOperation.Message) =>
                invalidOperation.Message,
            _ => "The operation could not be completed. Review the captured data and try again."
        };

    /// <summary>
    /// Represents UI input for creating orchestration version.
    /// </summary>
    public sealed class CreateVersionInput
    {
        [Obsolete("A semantic version is assigned only when an approved deployment review is applied.")]
        public string Version { get; set; } = string.Empty;

        [Obsolete("A version label is assigned only when an approved deployment review is applied.")]
        public string VersionLabel { get; set; } = string.Empty;

        [Obsolete("A version description is assigned only when an approved deployment review is applied.")]
        public string Description { get; set; } = string.Empty;

        [Obsolete("Deployment notes are assigned only when an approved deployment review is applied.")]
        public string Notes { get; set; } = string.Empty;

        [Obsolete("Draft identity is generated by the control plane.")]
        public string VersionId { get; set; } = string.Empty;

        [Obsolete("Draft checksums are generated by the control plane.")]
        public string Checksum { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets actor creating the version.
        /// </summary>
        public string CreatedBy { get; set; } = string.Empty;
    }

}
