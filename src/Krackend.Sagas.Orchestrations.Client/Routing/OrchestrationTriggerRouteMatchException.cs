namespace Krackend.Sagas.Orchestrations.Client.Routing;

/// <summary>
/// Represents a failure to resolve a required orchestration trigger route.
/// </summary>
public sealed class OrchestrationTriggerRouteMatchException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationTriggerRouteMatchException"/> class.
    /// </summary>
    /// <param name="message">Exception message.</param>
    public OrchestrationTriggerRouteMatchException(string message)
        : base(message)
    {
    }
}
