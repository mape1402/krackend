namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents trigger intake attempt in the orchestrator domain.
/// </summary>
public sealed class TriggerIntakeAttempt
{
    /// <summary>
    /// Gets or sets id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets trigger intake id.
    /// </summary>
    public Id TriggerIntakeId { get; set; }

    /// <summary>
    /// Gets or sets attempt number.
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Gets or sets action type.
    /// </summary>
    public required string ActionType { get; set; }

    /// <summary>
    /// Gets or sets outcome.
    /// </summary>
    public required string Outcome { get; set; }

    /// <summary>
    /// Gets or sets started on utc.
    /// </summary>
    public DateTime StartedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets finished on utc.
    /// </summary>
    public DateTime? FinishedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets error code.
    /// </summary>
    public string ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets error message.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets metadata.
    /// </summary>
    public Dictionary<string, JsonNode> Metadata { get; set; } = new();
}

