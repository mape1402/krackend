using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Assignments;

public sealed class IndexModel : PageModel
{
    private readonly IReleaseTargetApplicationService _service;
    public IndexModel(IReleaseTargetApplicationService service) { _service = service; }

    public IReadOnlyCollection<ReleaseTargetModel> Rows { get; private set; } = Array.Empty<ReleaseTargetModel>();
    public IReadOnlyCollection<ReleaseAttemptModel> Attempts { get; private set; } = Array.Empty<ReleaseAttemptModel>();
    [BindProperty(SupportsGet = true)] public string AssignmentId { get; set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        Rows = (await _service.GetAll(new ApplicationPagedSettings { PageNumber = 1, PageSize = 200 }, cancellationToken)).Rows;
        if (!string.IsNullOrWhiteSpace(AssignmentId))
        {
            Attempts = await _service.GetAttempts(AssignmentId, cancellationToken);
        }
    }
}

