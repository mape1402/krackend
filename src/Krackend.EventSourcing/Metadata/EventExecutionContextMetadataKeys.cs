namespace Krackend.EventSourcing.Metadata;

/// <summary>
/// Standard event execution context metadata keys.
/// </summary>
public static class EventExecutionContextMetadataKeys
{
    /// <summary>
    /// Metadata key for the correlation identifier.
    /// </summary>
    public const string CorrelationId = "correlationId";

    /// <summary>
    /// Metadata key for the causation identifier.
    /// </summary>
    public const string CausationId = "causationId";

    /// <summary>
    /// Metadata key for the user identifier.
    /// </summary>
    public const string UserId = "userId";

    /// <summary>
    /// Metadata key for the tenant identifier.
    /// </summary>
    public const string TenantId = "tenantId";

    /// <summary>
    /// Metadata key for the source component or channel.
    /// </summary>
    public const string Source = "source";
}
