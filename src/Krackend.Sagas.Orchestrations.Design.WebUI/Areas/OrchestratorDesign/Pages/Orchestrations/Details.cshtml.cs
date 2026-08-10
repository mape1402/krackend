using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Interaction;
using DesignPagedSettings = Krackend.Sagas.Orchestrations.Design.Interaction.InteractionPagedSettings;

namespace Krackend.Sagas.Orchestrations.Design.WebUI.Areas.OrchestratorDesign.Pages.Orchestrations;

/// <summary>
/// Represents orchestration details and version management.
/// </summary>
public sealed class DetailsModel : PageModel
{
    private readonly IOrchestrationInteractionService _orchestrationService;
    private readonly IOrchestrationVersionInteractionService _versionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DetailsModel"/> class.
    /// </summary>
    public DetailsModel(
        IOrchestrationInteractionService orchestrationService,
        IOrchestrationVersionInteractionService versionService)
    {
        _orchestrationService = orchestrationService ?? throw new ArgumentNullException(nameof(orchestrationService));
        _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
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

    /// <summary>
    /// Handles new version creation.
    /// </summary>
    public async Task<IActionResult> OnPostCreateVersionAsync(string orchestrationId, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(NewVersion.VersionId))
        {
            var updatedBy = string.IsNullOrWhiteSpace(NewVersion.CreatedBy) ? "web-ui" : NewVersion.CreatedBy;
            var checksum = string.IsNullOrWhiteSpace(NewVersion.Checksum) ? Guid.NewGuid().ToString("N") : NewVersion.Checksum;

            await _versionService.Update(
                new UpdateOrchestrationVersionCommand(
                    NewVersion.VersionId,
                    NewVersion.VersionLabel ?? string.Empty,
                    NewVersion.Description ?? string.Empty,
                    checksum,
                    NewVersion.Notes ?? string.Empty,
                    updatedBy),
                cancellationToken);

            return RedirectToPage("/Orchestrations/Details", new { area = "OrchestratorDesign", orchestrationId });
        }

        if (string.IsNullOrWhiteSpace(NewVersion.CreatedBy))
        {
            NewVersion.CreatedBy = "web-ui";
        }

        var versionId = await _versionService.Create(
            new CreateOrchestrationVersionCommand(
                orchestrationId,
                NewVersion.Version,
                OrchestrationVersionStatus.Draft,
                NewVersion.VersionLabel ?? string.Empty,
                NewVersion.Description ?? string.Empty,
                Guid.NewGuid().ToString("N"),
                NewVersion.Notes ?? string.Empty,
                NewVersion.CreatedBy),
            cancellationToken);

        return RedirectToPage("/OrchestrationVersions/Details", new { area = "OrchestratorDesign", orchestrationId, versionId });
    }

    /// <summary>
    /// Handles version status actions from the listing.
    /// </summary>
    public async Task<IActionResult> OnPostVersionActionAsync(string orchestrationId, string versionId, string action, CancellationToken cancellationToken = default)
    {
        const string actor = "web-ui";

        switch (action)
        {
            case "SetInReview":
                await _versionService.SetInReview(new SetOrchestrationVersionInReviewCommand(versionId), cancellationToken);
                break;
            case "ReturnToDraft":
                await _versionService.ReturnToDraft(new ReturnOrchestrationVersionToDraftCommand(versionId), cancellationToken);
                break;
            case "ReopenReview":
                await _versionService.ReopenReview(new ReopenOrchestrationVersionReviewCommand(versionId, actor), cancellationToken);
                break;
            case "Approve":
                await _versionService.Approve(new ApproveOrchestrationVersionCommand(versionId, actor), cancellationToken);
                break;
            case "Deploy":
                await _versionService.Deploy(new DeployOrchestrationVersionCommand(versionId, actor), cancellationToken);
                break;
            case "Deprecate":
                await _versionService.Deprecate(new DeprecateOrchestrationVersionCommand(versionId, actor), cancellationToken);
                break;
            case "Archive":
                await _versionService.Archive(new ArchiveOrchestrationVersionCommand(versionId, actor), cancellationToken);
                break;
        }

        return RedirectToPage("/Orchestrations/Details", new { area = "OrchestratorDesign", orchestrationId });
    }

    /// <summary>
    /// Returns allowed version actions by current status.
    /// </summary>
    public IEnumerable<string> GetAllowedActions(OrchestrationVersionStatus status)
    {
        return status switch
        {
            OrchestrationVersionStatus.Draft => new[] { "SetInReview", "Archive" },
            OrchestrationVersionStatus.InReview => new[] { "Approve", "ReturnToDraft", "Archive" },
            OrchestrationVersionStatus.Approved => new[] { "Deploy", "ReopenReview", "Archive" },
            OrchestrationVersionStatus.Deployed => new[] { "Deprecate", "Archive" },
            OrchestrationVersionStatus.Deprecated => new[] { "Archive" },
            _ => Array.Empty<string>()
        };
    }

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
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Represents UI input for creating orchestration version.
    /// </summary>
    public sealed class CreateVersionInput
    {
        /// <summary>
        /// Gets or sets the id for edit mode.
        /// </summary>
        public string VersionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets semantic version string.
        /// </summary>
        [Required]
        [Display(Name = "Version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets version label.
        /// </summary>
        [Display(Name = "Label")]
        public string VersionLabel { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets optional version description.
        /// </summary>
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets optional version notes.
        /// </summary>
        [Display(Name = "Notes")]
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets checksum for update mode.
        /// </summary>
        public string Checksum { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets actor creating the version.
        /// </summary>
        public string CreatedBy { get; set; } = string.Empty;
    }

}
