namespace Krackend.Sagas.Orchestrations.Client.Abstractions;

/// <summary>
/// Provides the current time for orchestration client services.
/// </summary>
public interface IOrchestrationClientClock
{
    /// <summary>
    /// Gets the current UTC instant.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}
