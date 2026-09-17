namespace Krackend.Sagas.Orchestrations.Runtime.Api;

/// <summary>
/// Represents a runtime design node enabled state request.
/// </summary>
public sealed record RuntimeDesignNodeEnabledRequest(bool IsEnabled);
