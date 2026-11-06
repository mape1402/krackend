namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon;

/// <summary>Limits how long a staged consumer can wait for confirmed readiness.</summary>
public sealed class PigeonConsumerReadinessOptions
{
    /// <summary>Readiness timeout in seconds. Values below one are treated as one second.</summary>
    public int TimeoutSeconds { get; set; } = 60;
}
