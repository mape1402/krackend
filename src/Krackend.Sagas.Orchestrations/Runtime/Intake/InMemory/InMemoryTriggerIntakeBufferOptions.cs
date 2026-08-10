namespace Krackend.Sagas.Orchestrations.Runtime.Intake.InMemory;

/// <summary>
/// Defines options for the in-memory trigger intake buffer.
/// </summary>
public sealed class InMemoryTriggerIntakeBufferOptions
{
    /// <summary>
    /// Gets or sets maximum queued items.
    /// </summary>
    public int Capacity { get; set; } = 10_000;
}
