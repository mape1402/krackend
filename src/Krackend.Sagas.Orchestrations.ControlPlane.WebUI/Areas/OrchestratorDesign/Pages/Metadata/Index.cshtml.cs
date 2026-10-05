using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.Metadata;

/// <summary>
/// Represents orchestration metadata descriptor management page.
/// </summary>
public sealed class IndexModel : PageModel
{
    private const int PageSize = 500;
    private const int MaxPageReads = 50;

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
    /// Gets listed metadata descriptors.
    /// </summary>
    public IReadOnlyCollection<MetadataDescriptorModel> Rows { get; private set; } = Array.Empty<MetadataDescriptorModel>();

    /// <summary>
    /// Handles page get request.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<MetadataDescriptorModel>();
        var pageNumber = 1;
        var totalPages = 1;

        do
        {
            var result = await _service.GetAll(
                new GetMetadataDescriptorsQuery(new ApplicationPagedSettings { PageNumber = pageNumber, PageSize = PageSize }),
                cancellationToken);
            rows.AddRange(result.Rows);
            totalPages = Math.Max(result.TotalPages, 1);
            pageNumber++;
        }
        while (pageNumber <= totalPages && pageNumber <= MaxPageReads);

        Rows = rows.ToArray();
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
