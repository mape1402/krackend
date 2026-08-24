using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Artifacts;

public sealed class IndexModel : PageModel
{
    private readonly IArtifactApplicationService _service;
    private readonly IOrchestrationNodePolicyApplicationService _policyService;

    public IndexModel(
        IArtifactApplicationService service,
        IOrchestrationNodePolicyApplicationService policyService)
    {
        _service = service;
        _policyService = policyService;
    }

    public IReadOnlyCollection<OrchestrationPolicyDefinitionModel> Orchestrations { get; private set; } = Array.Empty<OrchestrationPolicyDefinitionModel>();
    public IReadOnlyCollection<ArtifactModel> Rows { get; private set; } = Array.Empty<ArtifactModel>();
    [BindProperty(SupportsGet = true)] public string OrchestrationId { get; set; } = string.Empty;
    public OrchestrationPolicyDefinitionModel SelectedOrchestration { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        Orchestrations = await _policyService.GetOrchestrations(cancellationToken);
        var rows = (await _service.GetAll(new ApplicationPagedSettings { PageNumber = 1, PageSize = 300 }, cancellationToken)).Rows;
        if (string.IsNullOrWhiteSpace(OrchestrationId))
        {
            Rows = Array.Empty<ArtifactModel>();
            return;
        }

        SelectedOrchestration = Orchestrations.FirstOrDefault(x => string.Equals(x.Id, OrchestrationId, StringComparison.Ordinal));
        Rows = rows
            .Where(x => string.Equals(x.OrchestrationDefinitionId, OrchestrationId, StringComparison.Ordinal))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToArray();
    }
}

