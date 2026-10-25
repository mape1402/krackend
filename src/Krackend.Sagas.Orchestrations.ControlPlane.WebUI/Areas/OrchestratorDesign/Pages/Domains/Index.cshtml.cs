using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.Domains;

/// <summary>
/// Represents domain catalog management page.
/// </summary>
public sealed class IndexModel : PageModel
{
    private const int PageSize = 25;

    private readonly IDomainApplicationService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    /// <param name="service">Domain interaction service.</param>
    public IndexModel(IDomainApplicationService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>
    /// Gets or sets modal input.
    /// </summary>
    [BindProperty]
    public DomainInput Input { get; set; } = new();

    /// <summary>
    /// Handles page get request.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task OnGetAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>Loads a filtered page for the grid and cards views.</summary>
    public async Task<IActionResult> OnGetListAsync([FromQuery(Name = "page")] int page = 1, int pageSize = PageSize, string search = null,
        string sort = null, bool descending = false, CancellationToken cancellationToken = default)
    {
        var result = await GetPageAsync(page, pageSize, search, sort, descending, cancellationToken);
        return new JsonResult(new
        {
            items = result.Rows.Select(row => new
            {
                id = row.Id, key = row.Key, displayName = row.DisplayName, description = row.Description,
                isActive = row.IsActive, status = row.IsActive ? "Active" : "Inactive"
            }),
            totalCount = result.TotalRows,
            hasMore = result.PageNumber < result.TotalPages,
            search = search?.Trim() ?? string.Empty,
            sort = sort ?? string.Empty,
            descending
        });
    }

    private Task<ApplicationPagedResult<DomainModel>> GetPageAsync(int page, int pageSize, string search,
        string sort, bool descending, CancellationToken cancellationToken)
    {
        var field = sort switch
        {
            "key" => "key",
            "status" => "isActive",
            "description" => "description",
            _ => "displayName"
        };
        var sorts = new List<ApplicationSort> { new(field, descending) };
        if (field != "key") sorts.Add(new ApplicationSort("key", false));
        return _service.GetAll(new GetDomainsQuery(new ApplicationPagedSettings
        {
            PageNumber = Math.Max(page, 1),
            PageSize = Math.Clamp(pageSize, 1, 100),
            Sorts = sorts
        }, search?.Trim() ?? string.Empty), cancellationToken);
    }

    /// <summary>
    /// Handles upsert post request.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect result.</returns>
    public async Task<IActionResult> OnPostUpsertAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            await OnGetAsync(cancellationToken);
            return Page();
        }

        await _service.Upsert(new UpsertDomainCommand(
            Input.DomainId,
            Input.Key.Trim(),
            Input.DisplayName.Trim(),
            Input.Description?.Trim() ?? string.Empty), cancellationToken);

        return RedirectToPage();
    }

    /// <summary>
    /// Handles active state updates.
    /// </summary>
    /// <param name="domainId">Domain identifier.</param>
    /// <param name="isActive">New active flag value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect result.</returns>
    public async Task<IActionResult> OnPostSetIsActiveAsync(string domainId, bool isActive, CancellationToken cancellationToken = default)
    {
        await _service.SetIsActive(new SetDomainIsActiveCommand(domainId, isActive), cancellationToken);
        return RedirectToPage();
    }

    /// <summary>
    /// Represents domain modal input values.
    /// </summary>
    public sealed class DomainInput
    {
        /// <summary>
        /// Gets or sets domain id in edit mode.
        /// </summary>
        public string DomainId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets key.
        /// </summary>
        [Required]
        [MaxLength(128)]
        [RegularExpression(@"^[A-Za-z][A-Za-z0-9]*(?:[.-][A-Za-z0-9]+)*$", ErrorMessage = "Use letters or numbers separated by dot or dash, starting with a letter.")]
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
}
