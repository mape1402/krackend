using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Assignments;

public sealed class IndexModel : PageModel
{
    private readonly IReleaseTargetApplicationService _service;
    public IndexModel(IReleaseTargetApplicationService service) { _service = service; }

    [BindProperty(SupportsGet = true)] public string AssignmentId { get; set; } = string.Empty;

    public Task OnGetAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task<IActionResult> OnGetAttemptListAsync(string assignmentId, [FromQuery(Name = "page")] int page = 1,
        int pageSize = 25, string search = "", string sort = "", bool descending = false, CancellationToken cancellationToken = default)
    {
        if (!Ulid.TryParse(assignmentId, out _)) return BadRequest("Assignment id is invalid.");
        var term = search?.Trim() ?? string.Empty;
        var result = await _service.SearchAttempts(assignmentId, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), term, descending, cancellationToken);
        return new JsonResult(new
        {
            items = result.Rows.Select(x => new { id = x.Id, action = x.Action, initiatedBy = x.InitiatedBy,
                started = x.StartedAtUtc.ToString("yyyy-MM-dd HH:mm"), succeeded = x.Succeeded ? "Yes" : "No", error = x.ErrorMessage ?? string.Empty }),
            totalCount = result.TotalRows, hasMore = result.PageNumber < result.TotalPages,
            search = term, sort = sort ?? string.Empty, descending
        });
    }

    public async Task<IActionResult> OnGetListAsync([FromQuery(Name = "page")] int page = 1, int pageSize = 25,
        string search = "", string sort = "", bool descending = false, CancellationToken cancellationToken = default)
    {
        var term = search?.Trim() ?? string.Empty;
        var field = sort == "status" ? "status" : "assigned";
        var result = await _service.Search(Math.Max(1, page), Math.Clamp(pageSize, 1, 100), term, field, descending, cancellationToken);
        return new JsonResult(new
        {
            items = result.Rows.Select(x => new { id = x.Id, key = x.Id, displayName = string.IsNullOrWhiteSpace(x.RuntimeNodeName) ? x.RuntimeNodeId : x.RuntimeNodeName,
                runtime = string.IsNullOrWhiteSpace(x.RuntimeNodeName) ? x.RuntimeNodeId : x.RuntimeNodeName,
                release = x.ReleaseId, artifact = x.ArtifactId, status = x.Status, activation = x.ActivationStatus,
                assigned = x.AssignedAtUtc.ToString("yyyy-MM-dd HH:mm"),
                openUrl = Url.Page("/Assignments/Index", new { area = "OrchestratorDistribution", assignmentId = x.Id }) }),
            totalCount = result.TotalRows, hasMore = result.PageNumber < result.TotalPages,
            search = term, sort = sort ?? string.Empty, descending
        });
    }
}

