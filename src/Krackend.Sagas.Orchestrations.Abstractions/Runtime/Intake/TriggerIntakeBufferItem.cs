using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;

/// <summary>
/// Represents a trigger accepted by the runtime intake buffer.
/// </summary>
public sealed class TriggerIntakeBufferItem
{
    /// <summary>
    /// Gets or sets buffer item id.
    /// </summary>
    public Id BufferItemId { get; set; } = Id.New();

    /// <summary>
    /// Gets or sets trigger type.
    /// </summary>
    public TriggerType TriggerType { get; set; }

    /// <summary>
    /// Gets or sets trigger key.
    /// </summary>
    public required string TriggerKey { get; set; }

    /// <summary>
    /// Gets or sets the optional runtime artifact version requested by the trigger.
    /// When omitted, the active artifact for the trigger key is used.
    /// </summary>
    public string ArtifactVersion { get; set; }

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
    /// Gets or sets payload json.
    /// </summary>
    public required string PayloadJson { get; set; }

    /// <summary>
    /// Gets or sets received on utc.
    /// </summary>
    public DateTime ReceivedOnUtc { get; set; } = DateTime.UtcNow;
}
