using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;
using DesignPagedSettings = Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.ApplicationPagedSettings;
using SecurityPagedSettings = Krackend.Sagas.Orchestrations.ControlPlane.Application.Security.ApplicationPagedSettings;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.Orchestrations;

/// <summary>
/// Represents the orchestration listing page.
/// </summary>
public sealed class IndexModel : PageModel
{
    private readonly IOrchestrationApplicationService _orchestrationService;
    private readonly IDomainApplicationService _domainService;
    private readonly ITeamApplicationService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    /// <param name="orchestrationService">Orchestration interaction service.</param>
    public IndexModel(
        IOrchestrationApplicationService orchestrationService,
        IDomainApplicationService domainService,
        ITeamApplicationService teamService)
    {
        _orchestrationService = orchestrationService ?? throw new ArgumentNullException(nameof(orchestrationService));
        _domainService = domainService ?? throw new ArgumentNullException(nameof(domainService));
        _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
    }

    /// <summary>
    /// Gets the rows returned for the current page.
    /// </summary>
    public IReadOnlyCollection<OrchestrationDefinitionModel> Rows { get; private set; } = Array.Empty<OrchestrationDefinitionModel>();

    /// <summary>
    /// Gets the current page number.
    /// </summary>
    public int PageNumber { get; private set; } = 1;

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>
    /// Gets or sets new orchestration input values.
    /// </summary>
    [BindProperty]
    public CreateOrchestrationInput NewOrchestration { get; set; } = new();

    /// <summary>
    /// Gets a user-facing error message when listing fails.
    /// </summary>
    public string ErrorMessage { get; private set; } = string.Empty;

    /// <summary>
    /// Handles the GET request.
    /// </summary>
    /// <param name="pageNumber">Requested page number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task OnGetAsync(int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        await LoadPageAsync(pageNumber, cancellationToken);
    }

    /// <summary>
    /// Handles orchestration create or edit from the modal form.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect result.</returns>
    public async Task<IActionResult> OnPostUpsertAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            await LoadPageAsync(PageNumber, cancellationToken);
            return Page();
        }

        if (IsValidUlid(NewOrchestration.OrchestrationId))
        {
            await _orchestrationService.Update(
                new UpdateOrchestrationDefinitionCommand(
                    NewOrchestration.OrchestrationId,
                    NewOrchestration.Name.Trim(),
                    NewOrchestration.DomainId.Trim(),
                    NewOrchestration.Description?.Trim() ?? string.Empty,
                    NewOrchestration.OwnerTeamId.Trim(),
                    ParseTags(NewOrchestration.Tags),
                    NewOrchestration.UpdatedBy.Trim()),
                cancellationToken);

            return RedirectToPage("/Orchestrations/Index", new { area = "OrchestratorDesign" });
        }

        await _orchestrationService.Create(
            new CreateOrchestrationDefinitionCommand(
                NewOrchestration.Key.Trim(),
                NewOrchestration.Name.Trim(),
                NewOrchestration.DomainId.Trim(),
                NewOrchestration.Description?.Trim() ?? string.Empty,
                NewOrchestration.OwnerTeamId.Trim(),
                ParseTags(NewOrchestration.Tags),
                NewOrchestration.CreatedBy.Trim()),
            cancellationToken);

        return RedirectToPage("/Orchestrations/Index", new { area = "OrchestratorDesign" });
    }

    /// <summary>
    /// Handles orchestration activation from the listing menu.
    /// </summary>
    /// <param name="orchestrationId">Orchestration identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect result.</returns>
    public async Task<IActionResult> OnPostSetActiveAsync(string orchestrationId, CancellationToken cancellationToken = default)
    {
        await _orchestrationService.Activate(new ActivateOrchestrationDefinitionCommand(orchestrationId), cancellationToken);
        return RedirectToPage("/Orchestrations/Index", new { area = "OrchestratorDesign", pageNumber = PageNumber });
    }

    /// <summary>
    /// Handles orchestration deactivation from the listing menu.
    /// </summary>
    /// <param name="orchestrationId">Orchestration identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect result.</returns>
    public async Task<IActionResult> OnPostSetInactiveAsync(string orchestrationId, CancellationToken cancellationToken = default)
    {
        await _orchestrationService.Deactivate(new DeactivateOrchestrationDefinitionCommand(orchestrationId), cancellationToken);
        return RedirectToPage("/Orchestrations/Index", new { area = "OrchestratorDesign", pageNumber = PageNumber });
    }

    /// <summary>
    /// Returns domain suggestions for orchestration modal autocomplete.
    /// </summary>
    /// <param name="term">Optional search term.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Json suggestions list.</returns>
    public async Task<IActionResult> OnGetDomainSuggestionsAsync(string term = "", CancellationToken cancellationToken = default)
    {
        var rows = (await _domainService.GetAll(
            new GetDomainsQuery(
                new DesignPagedSettings { PageNumber = 1, PageSize = 20 },
                term ?? string.Empty),
            cancellationToken)).Rows
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(x => new
            {
                x.Id,
                x.Key,
                x.DisplayName,
                x.Description,
            });

        return new JsonResult(rows);
    }

    /// <summary>
    /// Returns team suggestions for orchestration modal autocomplete.
    /// </summary>
    /// <param name="term">Optional search term.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Json suggestions list.</returns>
    public async Task<IActionResult> OnGetTeamSuggestionsAsync(string term = "", CancellationToken cancellationToken = default)
    {
        var rows = (await _teamService.GetAll(
                new GetTeamsQuery(
                    new SecurityPagedSettings { PageNumber = 1, PageSize = 20 },
                    term ?? string.Empty),
                cancellationToken))
            .Rows
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(x => new
            {
                x.Id,
                x.Key,
                x.DisplayName,
            });

        return new JsonResult(rows);
    }

    private async Task LoadPageAsync(int pageNumber, CancellationToken cancellationToken)
    {
        PageNumber = pageNumber <= 0 ? 1 : pageNumber;

        try
        {
            var result = await _orchestrationService.GetAll(
                new GetOrchestrationDefinitionsQuery(
                    new DesignPagedSettings
                    {
                        PageNumber = PageNumber,
                        PageSize = 12
                    }),
                cancellationToken);

            Rows = result.Rows.ToArray();
            TotalPages = result.TotalPages <= 0 ? 1 : result.TotalPages;
            PageNumber = result.PageNumber <= 0 ? 1 : result.PageNumber;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private static IEnumerable<string> ParseTags(string tags)
    {
        return string.IsNullOrWhiteSpace(tags)
            ? Array.Empty<string>()
            : tags
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    private static bool IsValidUlid(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && Ulid.TryParse(value, out _);
    }

    /// <summary>
    /// Represents input values used to create an orchestration definition.
    /// </summary>
    public sealed class CreateOrchestrationInput
    {
        /// <summary>
        /// Gets or sets orchestration id for edit mode.
        /// </summary>
        public string OrchestrationId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the orchestration key.
        /// </summary>
        [Required]
        [MaxLength(128)]
        [RegularExpression(@"^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", ErrorMessage = "Use lowercase segments separated by dot or dash, starting with a letter.")]
        [Display(Name = "Key")]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the orchestration display name.
        /// </summary>
        [Required]
        [MaxLength(256)]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the business domain.
        /// </summary>
        [Required]
        [MaxLength(128)]
        [Display(Name = "Domain")]
        public string DomainText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets selected domain id.
        /// </summary>
        [Required]
        [Display(Name = "Domain")]
        public string DomainId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets optional description.
        /// </summary>
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets team display text.
        /// </summary>
        [Required]
        [Display(Name = "Owner team")]
        public string OwnerTeamText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets selected owner team identifier.
        /// </summary>
        [Required]
        [Display(Name = "Owner team")]
        public string OwnerTeamId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets comma-separated tags.
        /// </summary>
        [Display(Name = "Tags")]
        public string Tags { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets actor that creates the definition.
        /// </summary>
        [Required]
        [MaxLength(128)]
        public string CreatedBy { get; set; } = "web-ui";

        /// <summary>
        /// Gets or sets actor that updates the definition.
        /// </summary>
        [Required]
        [MaxLength(128)]
        public string UpdatedBy { get; set; } = "web-ui";
    }
}
