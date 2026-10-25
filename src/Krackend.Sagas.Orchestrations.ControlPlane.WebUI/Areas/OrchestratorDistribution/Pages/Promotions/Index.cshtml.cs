using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Releases;

public sealed class IndexModel : PageModel
{
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

    [BindProperty(SupportsGet = true)] public string OrchestrationId { get; set; } = string.Empty;
    public OrchestrationPolicyDefinitionModel SelectedOrchestration { get; private set; } = null!;
    [BindProperty] public ReleaseInput Input { get; set; } = new();
    [BindProperty] public AllowedNodesInput AllowedNodes { get; set; } = new();
    public ArtifactModel SelectedArtifact { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(OrchestrationId))
            await LoadPageData(cancellationToken);
    }

    public async Task<IActionResult> OnGetOrchestrationListAsync([FromQuery(Name = "page")] int page = 1, int pageSize = 25,
        string search = null, string sort = null, bool descending = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _policyService.SearchOrchestrations(new ApplicationPagedSettings
            {
                PageNumber = Math.Max(1, page), PageSize = Math.Clamp(pageSize, 1, 100)
            }, search?.Trim() ?? string.Empty, sort ?? string.Empty, descending, cancellationToken);
            var summaries = (await _promotionService.GetSummaries(result.Rows.Select(x => x.Id).ToArray(), cancellationToken))
                .ToDictionary(x => x.OrchestrationId, StringComparer.Ordinal);
            return new JsonResult(new
            {
                items = result.Rows.Select(item =>
                {
                    summaries.TryGetValue(item.Id, out var summary);
                    return new
                    {
                        id = item.Id, key = item.Key, displayName = item.Name, isActive = item.IsActive,
                        status = item.IsActive ? "Active" : "Inactive", count = summary?.Count ?? 0,
                        latest = summary?.LatestCreatedAtUtc.ToString("yyyy-MM-dd HH:mm") ?? string.Empty,
                        openUrl = Url.Page("/Promotions/Index", new { area = "OrchestratorDistribution", orchestrationId = item.Id })
                    };
                }).ToArray(),
                totalCount = result.TotalRows, hasMore = result.PageNumber < result.TotalPages,
                search = search?.Trim() ?? string.Empty, sort = sort ?? string.Empty, descending
            });
        }
        catch (NotSupportedException) { return StatusCode(503); }
    }

    public async Task<IActionResult> OnGetReleaseListAsync([FromQuery(Name = "page")] int page = 1, int pageSize = 25,
        string orchestrationId = null, string search = null, string sort = null, bool descending = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orchestrationId)) return BadRequest();
        try
        {
            var result = await _promotionService.Search(new ApplicationPagedSettings
            {
                PageNumber = Math.Max(1, page), PageSize = Math.Clamp(pageSize, 1, 100)
            }, orchestrationId, search?.Trim() ?? string.Empty, sort ?? string.Empty, descending, cancellationToken);
            var artifacts = (await _artifactService.GetSummariesByIds(result.Rows.Select(x => x.ArtifactId).Distinct().ToArray(), cancellationToken))
                .ToDictionary(x => x.Id, StringComparer.Ordinal);
            var targets = (await _releaseTargetService.GetByReleases(result.Rows.Select(x => x.Id).ToArray(), cancellationToken))
                .GroupBy(x => x.ReleaseId, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.GroupBy(y => y.RuntimeNodeId, StringComparer.Ordinal)
                    .Select(y => y.OrderByDescending(z => z.AssignedAtUtc).First()).ToArray(), StringComparer.Ordinal);
            return new JsonResult(new
            {
                items = result.Rows.Select(item =>
                {
                    artifacts.TryGetValue(item.ArtifactId, out var artifact);
                    targets.TryGetValue(item.Id, out var itemTargets);
                    var status = ResolveReleaseStatus(item, itemTargets ?? []);
                    var artifactLabel = artifact is null ? "No artifact linked" :
                        $"{artifact.ArtifactType} - {artifact.VersionNumber} - {(string.IsNullOrWhiteSpace(artifact.VersionLabel) ? "Unlabeled" : artifact.VersionLabel)}";
                    return new
                    {
                        id = item.Id, key = item.Id, displayName = "Release", artifact = artifactLabel,
                        status, strategy = item.Strategy, requestedBy = item.RequestedBy,
                        created = item.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm"), description = artifactLabel
                    };
                }).ToArray(),
                totalCount = result.TotalRows, hasMore = result.PageNumber < result.TotalPages,
                search = search?.Trim() ?? string.Empty, sort = sort ?? string.Empty, descending
            });
        }
        catch (NotSupportedException) { return StatusCode(503); }
    }

    private static string ResolveReleaseStatus(ReleaseModel release, IReadOnlyCollection<ReleaseTargetModel> targets)
    {
        var statuses = release.Targets.Select(plan => targets.FirstOrDefault(x => x.RuntimeNodeId == plan.RuntimeNodeId)?.Status ?? plan.Status)
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        if (statuses.Length == 0) return release.Status;
        if (statuses.All(x => string.Equals(x, "Activated", StringComparison.OrdinalIgnoreCase))) return "Completed";
        if (statuses.Any(x => string.Equals(x, "Failed", StringComparison.OrdinalIgnoreCase))) return "Failed";
        return release.Status;
    }

    public async Task<IActionResult> OnGetReleaseDetailsAsync(string releaseId, string orchestrationId, CancellationToken cancellationToken = default)
    {
        if (!Ulid.TryParse(releaseId, out _) || string.IsNullOrWhiteSpace(orchestrationId)) return BadRequest();
        try
        {
            var release = await _promotionService.GetById(releaseId, cancellationToken);
            if (release is null || !string.Equals(release.OrchestrationDefinitionId, orchestrationId, StringComparison.Ordinal)) return NotFound();
            var artifact = (await _artifactService.GetSummariesByIds([release.ArtifactId], cancellationToken)).FirstOrDefault();
            var assignments = (await _releaseTargetService.GetByRelease(releaseId, cancellationToken))
                .GroupBy(x => x.RuntimeNodeId, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.AssignedAtUtc).First(), StringComparer.Ordinal);
            var targets = new List<object>();
            foreach (var plan in release.Targets.OrderBy(x => x.CreatedAtUtc))
            {
                RuntimeNodeModel node = null;
                try { node = await _runtimeService.GetById(plan.RuntimeNodeId, cancellationToken); }
                catch (KeyNotFoundException) { }
                catch (InvalidOperationException) { }
                assignments.TryGetValue(plan.RuntimeNodeId, out var assignment);
                var status = assignment?.Status ?? plan.Status;
                var mode = node?.DistributionMode ?? string.Empty;
                var activated = string.Equals(status, "Activated", StringComparison.OrdinalIgnoreCase);
                targets.Add(new
                {
                    name = node?.Name ?? plan.RuntimeNodeId, code = node?.Code ?? "Unknown code",
                    environment = node?.EnvironmentName ?? "Unknown environment", mode,
                    status, targetId = assignment?.Id ?? string.Empty, activated,
                    canPush = !activated && assignment is not null && (mode == "DesignPublishesToRuntime" || mode == "HybridSync" || mode == "Push" || mode == "Hybrid"),
                    canPull = mode == "RuntimeFetchesFromDesign" || mode == "HybridSync" || mode == "Pull" || mode == "Hybrid"
                });
            }
            return new JsonResult(new
            {
                release.Id, orchestration = artifact?.OrchestrationDisplayName ?? "Release",
                artifact = artifact is null ? "No artifact linked" : $"{artifact.VersionNumber} - {(string.IsNullOrWhiteSpace(artifact.VersionLabel) ? "Unlabeled" : artifact.VersionLabel)}",
                created = release.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm"), targets
            });
        }
        catch (NotSupportedException) { return StatusCode(503); }
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken = default)
    {
        KeepModelStateFor("Input");

        if (string.IsNullOrWhiteSpace(Input.OrchestrationDefinitionId))
        {
            return RedirectToPage();
        }

        ArtifactModel selectedArtifact = null;
        try { selectedArtifact = await _artifactService.GetById(Input.ArtifactId, cancellationToken); }
        catch (FormatException) { }
        catch (ArgumentException) { }
        if (selectedArtifact is null || !string.Equals(selectedArtifact.OrchestrationDefinitionId, Input.OrchestrationDefinitionId, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "Selected artifact does not belong to the selected orchestration.");
            await LoadPageData(cancellationToken);
            return Page();
        }
        SelectedArtifact = selectedArtifact;

        var strategy = NormalizeReleaseStrategy(Input.Strategy);
        if (strategy is null)
        {
            ModelState.AddModelError(nameof(Input.Strategy), "Select Direct or Rollout as the release strategy.");
            await LoadPageData(cancellationToken);
            return Page();
        }

        if (string.Equals(strategy, ReleaseStrategy.Rollout.ToString(), StringComparison.Ordinal) &&
            !IsNewVersionArtifact(selectedArtifact.ArtifactType))
        {
            ModelState.AddModelError(nameof(Input.Strategy), "Rollout is available only when deploying a new orchestration version. Deprecation and archive artifacts use Direct.");
            await LoadPageData(cancellationToken);
            return Page();
        }

        var ids = Input.RuntimeNodeIds?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray() ?? Array.Empty<string>();
        if (ids.Length == 0)
        {
            ModelState.AddModelError(nameof(Input.RuntimeNodeIds), "Select at least one runtime node.");
            await LoadPageData(cancellationToken);
            return Page();
        }

        try
        {
            await _promotionService.Create(new CreateReleaseInput(Input.ArtifactId, Input.RequestedBy, strategy, ids, Input.Notes), cancellationToken);
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

    public async Task<IActionResult> OnGetArtifactOptionsAsync(string orchestrationId, [FromQuery(Name = "page")] int page = 1, string search = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orchestrationId)) return BadRequest();
        try
        {
            var result = await _artifactService.Search(new ApplicationPagedSettings { PageNumber = Math.Max(1, page), PageSize = 25 }, orchestrationId, search?.Trim() ?? string.Empty, "created", true, cancellationToken);
            return new JsonResult(new { items = result.Rows.Select(x => new { x.Id, x.ArtifactType, x.VersionNumber, x.VersionLabel }), hasMore = result.PageNumber < result.TotalPages });
        }
        catch (NotSupportedException) { return StatusCode(503); }
    }

    public async Task<IActionResult> OnGetNodeOptionsAsync(string orchestrationId, bool allowedOnly = false, [FromQuery(Name = "page")] int page = 1, string search = null, CancellationToken cancellationToken = default)
    {
        if (allowedOnly && string.IsNullOrWhiteSpace(orchestrationId)) return BadRequest();
        try
        {
            var settings = new ApplicationPagedSettings { PageNumber = Math.Max(1, page), PageSize = 25 };
            var result = allowedOnly
                ? await _runtimeService.SearchAllowed(settings, orchestrationId, search?.Trim() ?? string.Empty, cancellationToken)
                : await _runtimeService.SearchEnabled(settings, search?.Trim() ?? string.Empty, "name", false, cancellationToken);
            return new JsonResult(new { items = result.Rows.Select(x => new { x.Id, x.Name, x.Code }), hasMore = result.PageNumber < result.TotalPages });
        }
        catch (NotSupportedException) { return StatusCode(503); }
    }

    public async Task<IActionResult> OnPostSaveAllowedNodesAsync(CancellationToken cancellationToken = default)
    {
        KeepModelStateFor("AllowedNodes");

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
        var id = !string.IsNullOrWhiteSpace(OrchestrationId) ? OrchestrationId : Input.OrchestrationDefinitionId;
        if (!string.IsNullOrWhiteSpace(id))
        {
            OrchestrationId = id;
            SelectedOrchestration = await _policyService.GetOrchestration(id, cancellationToken);
        }
    }

    private void KeepModelStateFor(string prefix)
    {
        foreach (var key in ModelState.Keys.Where(key =>
                     !key.Equals(prefix, StringComparison.OrdinalIgnoreCase) &&
                     !key.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase)).ToArray())
            ModelState.Remove(key);
    }

    public sealed class ReleaseInput
    {
        [Required] public string OrchestrationDefinitionId { get; set; } = string.Empty;
        [Required] public string ArtifactId { get; set; } = string.Empty;
        public string RequestedBy { get; set; } = "web-ui";
        public string Strategy { get; set; } = nameof(ReleaseStrategy.Direct);
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

    private static string? NormalizeReleaseStrategy(string value)
    {
        return Enum.TryParse<ReleaseStrategy>(value, ignoreCase: true, out var strategy) &&
               (strategy == ReleaseStrategy.Direct || strategy == ReleaseStrategy.Rollout)
            ? strategy.ToString()
            : null;
    }

    private static bool IsNewVersionArtifact(string artifactType)
        => string.Equals(artifactType, "orchestration-version.new", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(artifactType, "orchestration-version-snapshot", StringComparison.OrdinalIgnoreCase);
}
