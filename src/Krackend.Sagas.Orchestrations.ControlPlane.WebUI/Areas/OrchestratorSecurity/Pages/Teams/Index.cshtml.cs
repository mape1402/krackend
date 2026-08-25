using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Security.Areas.OrchestratorSecurity.Pages.Teams;

/// <summary>
/// Represents teams management page.
/// </summary>
public sealed class IndexModel : PageModel
{
    private readonly ITeamApplicationService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    /// <param name="service">Team interaction service.</param>
    public IndexModel(ITeamApplicationService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>
    /// Gets team list rows.
    /// </summary>
    public IReadOnlyCollection<TeamModel> Rows { get; private set; } = Array.Empty<TeamModel>();

    /// <summary>
    /// Gets current team members loaded for modal.
    /// </summary>
    public IReadOnlyCollection<TeamMemberModel> TeamMembers { get; private set; } = Array.Empty<TeamMemberModel>();

    /// <summary>
    /// Gets selected team id for members view.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string TeamId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets team upsert input.
    /// </summary>
    [BindProperty]
    public TeamInput Input { get; set; } = new();

    /// <summary>
    /// Gets or sets add member input.
    /// </summary>
    [BindProperty]
    public AddMemberInput MemberInput { get; set; } = new();

    /// <summary>
    /// Handles get request.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        await LoadAsync(cancellationToken);
    }

    /// <summary>
    /// Handles team upsert.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect result.</returns>
    public async Task<IActionResult> OnPostUpsertAsync(CancellationToken cancellationToken = default)
    {
        ModelState.Remove("MemberInput.TeamId");
        ModelState.Remove("MemberInput.ExternalUserId");
        ModelState.Remove("MemberInput.DisplayName");

        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        await _service.Upsert(new UpsertTeamCommand(
            Input.TeamId,
            Input.Key.Trim(),
            Input.DisplayName.Trim(),
            Input.Description?.Trim() ?? string.Empty,
            "web-ui"), cancellationToken);

        return RedirectToPage();
    }

    /// <summary>
    /// Handles team active state update.
    /// </summary>
    /// <param name="teamId">Team id.</param>
    /// <param name="isActive">New active flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect result.</returns>
    public async Task<IActionResult> OnPostSetIsActiveAsync(string teamId, bool isActive, CancellationToken cancellationToken = default)
    {
        await _service.SetIsActive(new SetTeamIsActiveCommand(teamId, isActive, "web-ui"), cancellationToken);
        return RedirectToPage();
    }

    /// <summary>
    /// Handles add member request.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect result.</returns>
    public async Task<IActionResult> OnPostAddMemberAsync(CancellationToken cancellationToken = default)
    {
        ModelState.Remove("Input.TeamId");
        ModelState.Remove("Input.Key");
        ModelState.Remove("Input.DisplayName");
        ModelState.Remove("Input.Description");

        if (!ModelState.IsValid)
        {
            TeamId = MemberInput.TeamId;
            await LoadAsync(cancellationToken);
            return Page();
        }

        await _service.AddMember(new AddTeamMemberCommand(
            MemberInput.TeamId.Trim(),
            MemberInput.ExternalUserId.Trim(),
            MemberInput.DisplayName?.Trim() ?? string.Empty), cancellationToken);

        return RedirectToPage(new { teamId = MemberInput.TeamId });
    }

    /// <summary>
    /// Handles remove member request.
    /// </summary>
    /// <param name="teamId">Team id.</param>
    /// <param name="externalUserId">External user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect result.</returns>
    public async Task<IActionResult> OnPostRemoveMemberAsync(string teamId, string externalUserId, CancellationToken cancellationToken = default)
    {
        await _service.RemoveMember(new RemoveTeamMemberCommand(teamId, externalUserId), cancellationToken);
        return RedirectToPage(new { teamId });
    }

    /// <summary>
    /// Returns team members as JSON.
    /// </summary>
    /// <param name="teamId">Team id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Json payload.</returns>
    public async Task<IActionResult> OnGetMembersAsync(string teamId, CancellationToken cancellationToken = default)
    {
        var rows = await _service.GetMembers(new GetTeamMembersQuery(teamId), cancellationToken);
        return new JsonResult(rows);
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Rows = (await _service.GetAll(new GetTeamsQuery(
            new ApplicationPagedSettings { PageNumber = 1, PageSize = 200 }), cancellationToken)).Rows.ToArray();

        if (!string.IsNullOrWhiteSpace(TeamId))
        {
            TeamMembers = await _service.GetMembers(new GetTeamMembersQuery(TeamId), cancellationToken);
        }
    }

    /// <summary>
    /// Represents team modal input.
    /// </summary>
    public sealed class TeamInput
    {
        /// <summary>
        /// Gets or sets team id for edit mode.
        /// </summary>
        public string TeamId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets key.
        /// </summary>
        [Required]
        [MaxLength(128)]
        [RegularExpression(@"^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", ErrorMessage = "Use lowercase segments separated by dot or dash, starting with a letter.")]
        [Display(Name = "Key")]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets display name.
        /// </summary>
        [Required]
        [MaxLength(256)]
        [Display(Name = "Display name")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets description.
        /// </summary>
        [MaxLength(2048)]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents add member input.
    /// </summary>
    public sealed class AddMemberInput
    {
        /// <summary>
        /// Gets or sets team id.
        /// </summary>
        [Required]
        public string TeamId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets external user id.
        /// </summary>
        [Required]
        [MaxLength(256)]
        [Display(Name = "External user id")]
        public string ExternalUserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets display name.
        /// </summary>
        [MaxLength(256)]
        [Display(Name = "Display name")]
        public string DisplayName { get; set; } = string.Empty;
    }
}
