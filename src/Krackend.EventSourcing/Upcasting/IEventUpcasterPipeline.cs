namespace Krackend.EventSourcing.Upcasting;

/// <summary>
/// Applies configured event upcasters.
/// </summary>
public interface IEventUpcasterPipeline
{
    /// <summary>
    /// Upcasts payload to the requested version when required.
    /// </summary>
    UpcastedEventPayload Upcast(
        string eventType,
        string currentSchemaVersion,
        string targetSchemaVersion,
        string payload);
}
