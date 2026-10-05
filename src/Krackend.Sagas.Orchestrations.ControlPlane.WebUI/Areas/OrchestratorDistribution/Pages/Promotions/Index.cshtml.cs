using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Releases;

public sealed class IndexModel : PageModel
{
    private const int ReleasePageSize = 100;
    private const int ArtifactPageSize = 300;
    private const int RuntimeNodePageSize = 200;
    private const int ReleaseTargetPageSize = 1000;
    private const int MaxPageReads = 50;

    private readonly IReleaseApplicationService _promotionService;
    private readonly IReleaseTargetApplicationService _releaseTargetService;
    private readonly IArtifactApplicationService _artifactService;
    private readonly IRuntimeNodeApplicationService _runtimeService;
    private readonly IOrchestrationNodePolicyApplicationService _policyService;
    private readonly IArtifactDeliveryApplicationService _deliveryService;

    public IndexModel(
        IReleaseApplicationService promotionService,
        IReleaseTargetApplicationService releaseTargetService,
        IArtifactApplicationService artifactService,
        IRuntimeNodeApplicationService runtimeService,
        IOrchestrationNodePolicyApplicationService policyService,
        IArtifactDeliveryApplicationService deliveryService)
    {
        _promotionService = promotionService;
        _releaseTargetService = releaseTargetService;
        _artifactService = artifactService;
        _runtimeService = runtimeService;
        _policyService = policyService;
        _deliveryService = deliveryService;
    }

    public IReadOnlyCollection<ReleaseModel> Rows { get; private set; } = Array.Empty<ReleaseModel>();
    public IReadOnlyCollection<OrchestrationPolicyDefinitionModel> Orchestrations { get; private set; } = Array.Empty<OrchestrationPolicyDefinitionModel>();
    public IReadOnlyCollection<ArtifactModel> Artifacts { get; private set; } = Array.Empty<ArtifactModel>();
    public IReadOnlyCollection<ArtifactModel> SelectedArtifacts { get; private set; } = Array.Empty<ArtifactModel>();
    public IReadOnlyCollection<RuntimeNodeModel> RuntimeNodes { get; private set; } = Array.Empty<RuntimeNodeModel>();
    public IReadOnlyDictionary<string, ReleaseTargetModel> ReleaseTargetByReleaseAndNode { get; private set; } =
        new Dictionary<string, ReleaseTargetModel>(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> AllowedNodeIdsByOrchestrationId { get; private set; } =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, ReleaseOrchestrationSummary> ReleaseSummaryByOrchestrationId { get; private set; } =
        new Dictionary<string, ReleaseOrchestrationSummary>(StringComparer.Ordinal);
    [BindProperty(SupportsGet = true)] public string OrchestrationId { get; set; } = string.Empty;
    public OrchestrationPolicyDefinitionModel SelectedOrchestration { get; private set; } = null!;
    [BindProperty] public ReleaseInput Input { get; set; } = new();
    [BindProperty] public AllowedNodesInput AllowedNodes { get; set; } = new();

    public ReleaseOrchestrationSummary GetSummary(string orchestrationId)
        => !string.IsNullOrWhiteSpace(orchestrationId) &&
           ReleaseSummaryByOrchestrationId.TryGetValue(orchestrationId, out var summary)
            ? summary
            : ReleaseOrchestrationSummary.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        await LoadPageData(cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken = default)
    {
        ModelState.Remove("AllowedNodes.OrchestrationDefinitionId");
        ModelState.Remove("AllowedNodes.RuntimeNodeIds");
        ModelState.Remove("AllowedNodes.UpdatedBy");

        if (string.IsNullOrWhiteSpace(Input.OrchestrationDefinitionId))
        {
            return RedirectToPage();
        }

        var selectedArtifact = (await LoadAll(
                settings => _artifactService.GetAll(settings, cancellationToken),
                ArtifactPageSize))
            .FirstOrDefault(x => string.Equals(x.Id, Input.ArtifactId, StringComparison.Ordinal));
        if (selectedArtifact is null || !string.Equals(selectedArtifact.OrchestrationDefinitionId, Input.OrchestrationDefinitionId, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "Selected artifact does not belong to the selected orchestration.");
            await LoadPageData(cancellationToken);
            return Page();
        }

        var ids = Input.RuntimeNodeIds?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray() ?? Array.Empty<string>();
        if (ids.Length == 0)
        {
            ModelState.AddModelError(nameof(Input.RuntimeNodeIds), "Select at least one runtime node.");
            await OnGetAsync(cancellationToken);
            return Page();
        }

        try
        {
            await _promotionService.Create(new CreateReleaseInput(Input.ArtifactId, Input.RequestedBy, Input.Strategy, ids, Input.Notes), cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadPageData(cancellationToken);
            return Page();
        }

        return RedirectToPage(new { OrchestrationId = Input.OrchestrationDefinitionId });
    }

    public async Task<IActionResult> OnPostPushAsync(
        string releaseTargetId,
        string orchestrationId,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(releaseTargetId))
        {
            try
            {
                var result = await _deliveryService.Push(releaseTargetId, "web-ui", cancellationToken);
                SetMessage(
                    result.Succeeded ? "Artifact promoted" : "Artifact promotion failed",
                    result.Message,
                    result.Succeeded ? "success" : "error");
            }
            catch (Exception ex)
            {
                SetMessage("Artifact promotion failed", ex.Message, "error");
            }
        }

        return RedirectToPage(new { OrchestrationId = orchestrationId });
    }

    private void SetMessage(string title, string body, string type)
    {
        TempData["OrchestratorMessage.Title"] = title;
        TempData["OrchestratorMessage.Body"] = body;
        TempData["OrchestratorMessage.Type"] = type;
    }

    public async Task<IActionResult> OnGetAllowedNodesAsync(string orchestrationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orchestrationId))
        {
            return new JsonResult(new { orchestrationId = string.Empty, runtimeNodeIds = Array.Empty<string>() });
        }

        var policy = await _policyService.Get(orchestrationId, cancellationToken);
        return new JsonResult(new { orchestrationId, runtimeNodeIds = policy.RuntimeNodeIds });
    }

    public async Task<IActionResult> OnPostSaveAllowedNodesAsync(CancellationToken cancellationToken = default)
    {
        ModelState.Remove("Input.OrchestrationDefinitionId");
        ModelState.Remove("Input.ArtifactId");
        ModelState.Remove("Input.RequestedBy");
        ModelState.Remove("Input.Strategy");
        ModelState.Remove("Input.RuntimeNodeIds");
        ModelState.Remove("Input.Notes");

        if (string.IsNullOrWhiteSpace(AllowedNodes.OrchestrationDefinitionId))
        {
            ModelState.AddModelError(string.Empty, "Select an orchestration first to configure allowed nodes.");
            await LoadPageData(cancellationToken);
            return Page();
        }

        await _policyService.Replace(new ReplaceOrchestrationNodePolicyInput(
            AllowedNodes.OrchestrationDefinitionId,
            AllowedNodes.RuntimeNodeIds?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray() ?? Array.Empty<string>(),
            string.IsNullOrWhiteSpace(AllowedNodes.UpdatedBy) ? "web-ui" : AllowedNodes.UpdatedBy),
            cancellationToken);

        return RedirectToPage(new { OrchestrationId = AllowedNodes.OrchestrationDefinitionId });
    }

    private async Task LoadPageData(CancellationToken cancellationToken)
    {
        var allRows = await LoadAll(
            settings => _promotionService.GetAll(settings, cancellationToken),
            ReleasePageSize);
        Orchestrations = await _policyService.GetOrchestrations(cancellationToken);
        Artifacts = await LoadAll(
            settings => _artifactService.GetAll(settings, cancellationToken),
            ArtifactPageSize);
        ReleaseSummaryByOrchestrationId = allRows
            .Where(x => !string.IsNullOrWhiteSpace(x.OrchestrationDefinitionId))
            .GroupBy(x => x.OrchestrationDefinitionId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => new ReleaseOrchestrationSummary(g.Count(), g.Max(x => x.CreatedAtUtc)),
                StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(OrchestrationId))
        {
            Rows = Array.Empty<ReleaseModel>();
            SelectedArtifacts = Array.Empty<ArtifactModel>();
        }
        else
        {
            SelectedOrchestration = Orchestrations.FirstOrDefault(x => string.Equals(x.Id, OrchestrationId, StringComparison.Ordinal));
            Rows = allRows
                .Where(x => string.Equals(x.OrchestrationDefinitionId, OrchestrationId, StringComparison.Ordinal))
                .ToArray();
            SelectedArtifacts = Artifacts
                .Where(x => string.Equals(x.OrchestrationDefinitionId, OrchestrationId, StringComparison.Ordinal))
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToArray();
        }
        RuntimeNodes = (await LoadAll(
                settings => _runtimeService.GetAll(settings, cancellationToken),
                RuntimeNodePageSize))
            .Where(x => !x.IsDeleted && string.Equals(x.Status, RuntimeNodeStatus.Enabled.ToString(), StringComparison.Ordinal))
            .ToArray();
        var releaseIds = Rows.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var releaseTargets = (await LoadAll(
                settings => _releaseTargetService.GetAll(settings, cancellationToken),
                ReleaseTargetPageSize))
            .Where(x => !string.IsNullOrWhiteSpace(x.ReleaseId) && releaseIds.Contains(x.ReleaseId))
            .ToArray();
        ReleaseTargetByReleaseAndNode = releaseTargets
            .GroupBy(x => $"{x.ReleaseId}|{x.RuntimeNodeId}", StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.AssignedAtUtc).First(),
                StringComparer.Ordinal);

        var orchestrationIds = Artifacts
            .Select(x => x.OrchestrationDefinitionId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        AllowedNodeIdsByOrchestrationId = await _policyService.GetByOrchestrationIds(orchestrationIds, cancellationToken);
    }

    private static async Task<TModel[]> LoadAll<TModel>(
        Func<ApplicationPagedSettings, Task<ApplicationPagedResult<TModel>>> loadPage,
        int pageSize)
    {
        var rows = new List<TModel>();
        var pageNumber = 1;
        var totalPages = 1;

        do
        {
            var result = await loadPage(new ApplicationPagedSettings
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            });
            rows.AddRange(result.Rows);
            totalPages = Math.Max(result.TotalPages, 1);
            pageNumber++;
        }
        while (pageNumber <= totalPages && pageNumber <= MaxPageReads);

        return rows.ToArray();
    }

    public sealed class ReleaseInput
    {
        [Required] public string OrchestrationDefinitionId { get; set; } = string.Empty;
        [Required] public string ArtifactId { get; set; } = string.Empty;
        public string RequestedBy { get; set; } = "web-ui";
        public string Strategy { get; set; } = "Immediate";
        public string[] RuntimeNodeIds { get; set; } = Array.Empty<string>();
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class AllowedNodesInput
    {
        [Required] public string OrchestrationDefinitionId { get; set; } = string.Empty;
        public string[] RuntimeNodeIds { get; set; } = Array.Empty<string>();
        public string UpdatedBy { get; set; } = "web-ui";
    }

    public sealed record ReleaseOrchestrationSummary(int ReleaseCount, DateTime? LatestCreatedAtUtc)
    {
        public static ReleaseOrchestrationSummary Empty { get; } = new(0, null);
    }
}
