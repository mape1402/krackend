using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.Distribution.Interaction;

namespace Krackend.Sagas.Orchestrations.Distribution.WebUI.Areas.OrchestratorDistribution.Pages.Assignments;

public sealed class IndexModel : PageModel
{
    private readonly IReleaseTargetInteractionService _service;
    public IndexModel(IReleaseTargetInteractionService service) { _service = service; }

    public IReadOnlyCollection<ReleaseTargetModel> Rows { get; private set; } = Array.Empty<ReleaseTargetModel>();
    public IReadOnlyCollection<ReleaseAttemptModel> Attempts { get; private set; } = Array.Empty<ReleaseAttemptModel>();
    [BindProperty(SupportsGet = true)] public string AssignmentId { get; set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        Rows = (await _service.GetAll(new InteractionPagedSettings { PageNumber = 1, PageSize = 200 }, cancellationToken)).Rows;
        if (!string.IsNullOrWhiteSpace(AssignmentId))
        {
            Attempts = await _service.GetAttempts(AssignmentId, cancellationToken);
        }
    }
}

