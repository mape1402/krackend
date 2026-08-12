using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Describes a compensation action to schedule for a completed source task.
/// </summary>
/// <param name="SourceTaskExecutionId">Completed source task execution id.</param>
/// <param name="SourceTaskKey">Completed source task key.</param>
/// <param name="CompensationTaskKey">Synthetic compensation task key.</param>
/// <param name="Kind">Compensation task kind.</param>
/// <param name="DispatchType">Compensation dispatch type.</param>
/// <param name="Destination">Transport destination.</param>
/// <param name="MessageVersion">Message version.</param>
/// <param name="RequestPayload">Compensation request payload.</param>
/// <param name="Metadata">Compensation metadata.</param>
internal sealed record RuntimeCompensationPlanItem(
    Id SourceTaskExecutionId,
    string SourceTaskKey,
    string CompensationTaskKey,
    string Kind,
    string DispatchType,
    string Destination,
    string MessageVersion,
    JsonNode RequestPayload,
    Dictionary<string, JsonNode> Metadata);
