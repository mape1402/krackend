namespace Krackend.Sagas.Orchestrations.Runtime.Diagnostics;

/// <summary>Optional diagnostics reader that loads only the related history needed by a detail section.</summary>
public interface IRuntimeSectionDiagnosticsReader
{
    /// <summary>Gets instance detail for a UI section without unrelated transition or compensation history.</summary>
    Task<InstanceDetailModel> GetDetailForSection(string instanceId, string section, CancellationToken cancellationToken = default, RuntimeDetailSelection selection = null);
}

/// <summary>Identifies the execution being inspected, when a detail section needs its payloads.</summary>
public sealed record RuntimeDetailSelection(string StageKey, string TaskKey, string StageExecutionId, string TaskExecutionId, int? Attempt);
