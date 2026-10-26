using Krackend.Sagas.Orchestrations.Runtime.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Instances;

/// <summary>Displays the persisted trace of a single orchestration instance.</summary>
public sealed class DetailModel : PageModel
{
    private readonly IRuntimeDiagnosticsReader _diagnostics;

    public DetailModel(IRuntimeDiagnosticsReader diagnostics) => _diagnostics = diagnostics;

    public InstanceDetailModel Detail { get; private set; }

    public string Section { get; private set; } = "execution";

    public StageDetailModel Stage { get; private set; }

    public TaskDetailModel Task { get; private set; }

    public TaskAttemptDetailModel Attempt { get; private set; }

    public async Task<IActionResult> OnGetAsync(string instanceId, CancellationToken cancellationToken = default, string section = null, string stageKey = null, string taskKey = null, int? attempt = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || !Ulid.TryParse(instanceId, out _))
            return NotFound();

        try
        {
            Detail = await _diagnostics.GetDetail(instanceId, cancellationToken);
            if (Detail is null) return NotFound();

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
}
