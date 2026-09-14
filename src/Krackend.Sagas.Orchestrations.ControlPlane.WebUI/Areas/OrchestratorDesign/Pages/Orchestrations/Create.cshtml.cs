using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;
using DesignPagedSettings = Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.ApplicationPagedSettings;
using SecurityPagedSettings = Krackend.Sagas.Orchestrations.ControlPlane.Application.Security.ApplicationPagedSettings;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.Orchestrations;

/// <summary>
/// Represents the orchestration creation page.
/// </summary>
public sealed class CreateModel : PageModel
{
    private readonly IOrchestrationApplicationService _orchestrationService;
    private readonly IDomainApplicationService _domainService;
    private readonly ITeamApplicationService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateModel"/> class.
    /// </summary>
    /// <param name="orchestrationService">Orchestration interaction service.</param>
    public CreateModel(
        IOrchestrationApplicationService orchestrationService,
        IDomainApplicationService domainService,
        ITeamApplicationService teamService)
    {
        _orchestrationService = orchestrationService ?? throw new ArgumentNullException(nameof(orchestrationService));
        _domainService = domainService ?? throw new ArgumentNullException(nameof(domainService));
        _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
    }

    /// <summary>
    /// Gets or sets the user input for creating an orchestration.
    /// </summary>
    [BindProperty]
    public CreateOrchestrationInput Input { get; set; } = new();

    /// <summary>
    /// Gets the error message to display when command execution fails.
    /// </summary>
    public string ErrorMessage { get; private set; } = string.Empty;

    /// <summary>
    /// Handles the POST request.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirects to detail page when successful; otherwise returns current page.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var orchestrationId = await _orchestrationService.Create(
                new CreateOrchestrationDefinitionCommand(
                    Input.Key.Trim(),
                    Input.Name.Trim(),
                    Input.DomainId.Trim(),
                    Input.Description?.Trim() ?? string.Empty,
                    Input.OwnerTeamId.Trim(),
                    ParseTags(Input.Tags),
                    Input.CreatedBy.Trim()),
                cancellationToken);

            return RedirectToPage("/Orchestrations/Details", new { area = "OrchestratorDesign", orchestrationId });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }

    /// <summary>
    /// Returns domain suggestions for autocomplete.
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
    /// Returns team suggestions for autocomplete.
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

    private static IEnumerable<string> ParseTags(string tags)
    {
        return string.IsNullOrWhiteSpace(tags)
            ? Array.Empty<string>()
            : tags
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    /// <summary>
    /// Represents input values used to create an orchestration definition.
    /// </summary>
    public sealed class CreateOrchestrationInput
    {
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
        /// Gets or sets an optional description.
        /// </summary>
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the owner team.
        /// </summary>
        [Display(Name = "Owner team")]
        public string OwnerTeamText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets selected owner team id.
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
        /// Gets or sets the actor creating the definition.
        /// </summary>
        [Required]
        [MaxLength(128)]
        [Display(Name = "Created by")]
        public string CreatedBy { get; set; } = "web-ui";
    }
}
