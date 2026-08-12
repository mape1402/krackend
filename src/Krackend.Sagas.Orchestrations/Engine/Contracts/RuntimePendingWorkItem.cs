using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Describes runtime work found by a pending-work scan.
/// </summary>
/// <param name="WorkType">Pending work type.</param>
/// <param name="Id">Work item id.</param>
/// <param name="OrchestrationInstanceId">Owning orchestration instance id when available.</param>
/// <param name="TaskExecutionId">Task execution id when available.</param>
/// <param name="DueOnUtc">Due timestamp.</param>
/// <param name="Status">Current work status.</param>
public sealed record RuntimePendingWorkItem(
    string WorkType,
    Id Id,
    Id? OrchestrationInstanceId,
    Id? TaskExecutionId,
    DateTime? DueOnUtc,
    string Status);
