namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents trigger intake in the orchestrator domain.
/// </summary>
public sealed class TriggerIntake
{
    /// <summary>
    /// Gets or sets id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets trigger type.
    /// </summary>
    public TriggerType TriggerType { get; set; }

    /// <summary>
    /// Gets or sets trigger key.
    /// </summary>
    public required string TriggerKey { get; set; }

    /// <summary>
    /// Gets or sets environment key.
    /// </summary>
    public required string EnvironmentKey { get; set; }

    /// <summary>
    /// Gets or sets correlation id.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets idempotency key.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets source message id.
    /// </summary>
    public string SourceMessageId { get; set; }

    /// <summary>
    /// Gets or sets source request id.
    /// </summary>
    public string SourceRequestId { get; set; }

    /// <summary>
    /// Gets or sets raw payload.
    /// </summary>
    public required JsonNode RawPayload { get; set; }

    /// <summary>
    /// Gets or sets normalized payload.
    /// </summary>
    public JsonNode NormalizedPayload { get; set; }

    /// <summary>
    /// Gets or sets status.
    /// </summary>
    public TriggerIntakeStatus Status { get; set; }

    /// <summary>
    /// Gets or sets persistence level.
    /// </summary>
    public string PersistenceLevel { get; set; }

    /// <summary>
    /// Gets or sets buffer location.
    /// </summary>
    public string BufferLocation { get; set; }

    /// <summary>
    /// Gets or sets resolved artifact id.
    /// </summary>
    public Id? ResolvedArtifactId { get; set; }

    /// <summary>
    /// Gets or sets promoted instance id.
    /// </summary>
    public Id? PromotedInstanceId { get; set; }

    /// <summary>
    /// Gets or sets received on utc.
    /// </summary>
    public DateTime ReceivedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets promoted on utc.
    /// </summary>
    public DateTime? PromotedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets expires on utc.
    /// </summary>
    public DateTime? ExpiresOnUtc { get; set; }

    /// <summary>
    /// Gets or sets rejection reason.
    /// </summary>
    public string RejectionReason { get; set; }

    /// <summary>
    /// Gets or sets failure reason.
    /// </summary>
    public string FailureReason { get; set; }
}
