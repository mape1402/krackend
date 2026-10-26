namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

/// <summary>Configures deferred statistics batches.</summary>
public sealed class SagaStatisticsMuleOptions
{
    /// <summary>Gets or sets whether statistics batches are scheduled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the scheduling interval in seconds.</summary>
    public int IntervalSeconds { get; set; } = 10;

    /// <summary>Gets or sets the maximum observations processed in one batch.</summary>
    public int BatchSize { get; set; } = 200;
}
