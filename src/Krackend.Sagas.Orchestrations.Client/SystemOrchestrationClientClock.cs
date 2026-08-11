using Krackend.Sagas.Orchestrations.Client.Abstractions;

namespace Krackend.Sagas.Orchestrations.Client;

/// <summary>
/// System clock implementation for orchestration client services.
/// </summary>
public sealed class SystemOrchestrationClientClock : IOrchestrationClientClock
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
