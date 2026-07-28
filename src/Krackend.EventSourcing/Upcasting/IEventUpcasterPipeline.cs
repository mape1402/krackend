namespace Krackend.EventSourcing.Upcasting;

using Krackend.EventSourcing.Contracts;

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
        SemanticVersion currentSchemaVersion,
        SemanticVersion targetSchemaVersion,
        string payload);
}
