using Krackend.Sagas.Orchestrations.Runtime.Diagnostics;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Instances;

/// <summary>Displays the persisted trace of a single orchestration instance.</summary>
public sealed class DetailModel : PageModel
{
    private readonly IRuntimeDiagnosticsReader _diagnostics;
    private readonly IOrchestrationRecoveryService _recovery;
    private readonly OrchestratorRuntimeWebUIOptions _options;
    private readonly IOrchestrationReconciliationService _reconciliation;

    public DetailModel(
        IRuntimeDiagnosticsReader diagnostics,
        IOrchestrationRecoveryService recovery,
        IOptions<OrchestratorRuntimeWebUIOptions> options,
        IOrchestrationReconciliationService reconciliation = null)
    {
        _diagnostics = diagnostics;
        _recovery = recovery;
        _options = options.Value;
        _reconciliation = reconciliation;
    }

    public InstanceDetailModel Detail { get; private set; }

    public string Section { get; private set; } = "execution";

    public StageDetailModel Stage { get; private set; }

    public TaskDetailModel Task { get; private set; }

    public TaskAttemptDetailModel Attempt { get; private set; }

    /// <summary>Gets the trace identifier when it was supplied by trigger metadata.</summary>
    public string TraceId { get; private set; } = string.Empty;

    /// <summary>Gets the SignalR hub route used for unobtrusive detail freshness notifications.</summary>
    public string LiveHubPath => $"/{(string.IsNullOrWhiteSpace(_options.RoutePrefix) ? "runtime" : _options.RoutePrefix.Trim('/'))}/live";

    /// <summary>Gets whether the persisted terminal instance can be recovered by an operator.</summary>
    public bool CanRecover => CanRecoverInstance(Detail);

    private static bool CanRecoverInstance(InstanceDetailModel detail)
        => detail?.Instance?.Status is "DeadLettered" or "Failed" ||
            (detail?.Instance?.Status == "Completed" && detail.Tasks?.Any(task =>
                !string.IsNullOrWhiteSpace(task.ParallelGroupId) &&
                task.Status is "Cancelled" or "WaitingResponse") == true);

    /// <summary>Gets whether the active saga can be inspected for missing work.</summary>
    public bool CanReconcile => Detail?.Instance?.Status is "Running" or "Idle" or "Compensating";

    /// <summary>Gets the non-sensitive result of the latest recovery request.</summary>
    [TempData]
    public string RecoveryMessage { get; set; }

    /// <summary>Diagnoses and reconciles the selected active saga.</summary>
    public async Task<IActionResult> OnPostReconcileAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || !Ulid.TryParse(instanceId, out _)) return NotFound();
        if (!string.IsNullOrWhiteSpace(_options.ManagePolicy))
        {
            var authorization = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
            if (!(await authorization.AuthorizeAsync(User, null, _options.ManagePolicy)).Succeeded) return Forbid();
        }
        if (_reconciliation is null) throw new InvalidOperationException("Saga reconciliation is not configured.");
        var result = await _reconciliation.ReconcileAsync(instanceId, cancellationToken);
        if (result.Status == "NotFound") return NotFound();
        RecoveryMessage = string.Join(" ", result.Findings.Select(finding => $"[{finding.Status}] {finding.Message}"));
        if (RecoveryMessage.Length > 2000)
            RecoveryMessage = RecoveryMessage[..1900] + "... Full findings are available in the reconciliation activity payload.";
        if (PageContext?.HttpContext is not null)
            TempData["RecoveryTone"] = result.Status == "Recovered" ? "success" : result.Status is "Intervention" or "Invalid" ? "danger" : "neutral";
        return RedirectToPage(new { instanceId });
    }

    public async Task<IActionResult> OnGetAsync(string instanceId, CancellationToken cancellationToken = default, string section = null, string stageKey = null, string taskKey = null, int? attempt = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || !Ulid.TryParse(instanceId, out _))
            return NotFound();

        try
        {
            Detail = await _diagnostics.GetDetail(instanceId, cancellationToken);
            if (Detail is null) return NotFound();
            TraceId = FindString(JsonNode.Parse(Detail.Metadata), "TraceId");

            Section = string.IsNullOrEmpty(section) ? "execution" : section.ToLowerInvariant();
            if (Section is not ("overview" or "activity" or "execution" or "data" or "stage" or "task" or "attempt" or "compensations")) return NotFound();
            if (Section is "stage" or "task" or "attempt")
            {
                Stage = Detail.Stages.FirstOrDefault(x => string.Equals(x.StageKey, stageKey, StringComparison.Ordinal));
                if (Stage is null) return NotFound();
            }
            if (Section is "task" or "attempt")
            {
                Task = Stage.Tasks.FirstOrDefault(x => string.Equals(x.TaskKey, taskKey, StringComparison.Ordinal));
                if (Task is null) return NotFound();
            }
            if (Section is "attempt")
            {
                Attempt = Task.Attempts.FirstOrDefault(x => x.AttemptNumber == attempt);
                if (Attempt is null) return NotFound();
            }
            return Page();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Requests terminal-saga recovery from the instance detail rather than from the grid.</summary>
    public async Task<IActionResult> OnPostRecoverAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || !Ulid.TryParse(instanceId, out _))
            return NotFound();

        InstanceDetailModel current;
        try
        {
            current = await _diagnostics.GetDetail(instanceId, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        if (!CanRecoverInstance(current))
        {
            RecoveryMessage = "This saga has already changed state and cannot be recovered from this view.";
            if (PageContext?.HttpContext is not null) TempData["RecoveryTone"] = "danger";
            return RedirectToPage(new { instanceId });
        }

        var result = await _recovery.RecoverTerminalAsync(instanceId, null, cancellationToken);
        RecoveryMessage = result.Message;
        if (PageContext?.HttpContext is not null)
        {
            TempData["RecoveryTone"] = result.Succeeded ? "success" : "danger";
        }
        return RedirectToPage(new { instanceId });
    }

    /// <summary>Retained for existing detail links; uses the terminal recovery guard.</summary>
    public Task<IActionResult> OnPostReplayAsync(string instanceId, string reason = null, CancellationToken cancellationToken = default)
        => OnPostRecoverAsync(instanceId, cancellationToken);

    private static string FindString(JsonNode node, string name)
    {
        if (node is JsonObject obj)
        {
            foreach (var pair in obj)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase) &&
                    pair.Value is JsonValue value && value.TryGetValue<string>(out var text))
                    return text ?? string.Empty;

                var nested = FindString(pair.Value, name);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                var nested = FindString(item, name);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        }

        return string.Empty;
    }
}
