using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.Metadata;

/// <summary>
/// Represents orchestration metadata descriptor management page.
/// </summary>
public sealed class IndexModel : PageModel
{
    private const int PageSize = 25;

    private readonly IMetadataDescriptorApplicationService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    /// <param name="service">Metadata descriptor interaction service.</param>
    public IndexModel(IMetadataDescriptorApplicationService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>
    /// Handles page get request.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task OnGetAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>Loads a filtered page without transmitting schema snapshots to the catalog.</summary>
    public async Task<IActionResult> OnGetListAsync([FromQuery(Name = "page")] int page = 1, int pageSize = PageSize,
        string search = null, string sort = null, bool descending = false, CancellationToken cancellationToken = default)
    {
        var field = sort switch
        {
            "key" => "key",
            "sourceKey" => "sourceKey",
            "description" => "description",
            _ => "displayName"
        };
        var sorts = new List<ApplicationSort> { new(field, descending) };
        if (field != "key") sorts.Add(new ApplicationSort("key", false));
        var result = await _service.GetAll(new GetMetadataDescriptorsQuery(new ApplicationPagedSettings
        {
            PageNumber = Math.Max(page, 1), PageSize = Math.Clamp(pageSize, 1, 100), Sorts = sorts
        }, search?.Trim() ?? string.Empty), cancellationToken);
        return new JsonResult(new
        {
            items = result.Rows.Select(row => new
            {
                id = row.Id, key = row.Key, sourceKey = row.SourceKey,
                displayName = row.DisplayName, description = row.Description
            }),
            totalCount = result.TotalRows,
            hasMore = result.PageNumber < result.TotalPages,
            search = search?.Trim() ?? string.Empty,
            sort = sort ?? string.Empty,
            descending
        });
    }

    /// <summary>
    /// Handles metadata descriptor delete requests.
    /// </summary>
    /// <param name="metadataDescriptorId">Descriptor identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect result.</returns>
    public async Task<IActionResult> OnPostDeleteAsync(
        string metadataDescriptorId,
        CancellationToken cancellationToken = default)
    {
        await _service.Delete(new DeleteMetadataDescriptorCommand(metadataDescriptorId), cancellationToken);
        return RedirectToPage();
    }

}
