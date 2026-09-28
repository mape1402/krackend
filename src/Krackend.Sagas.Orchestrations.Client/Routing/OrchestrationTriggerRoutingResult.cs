namespace Krackend.Sagas.Orchestrations.Client.Routing;

using Krackend.Sagas.Orchestrations.Client.Publishing;

/// <summary>
/// Contains the payload and trigger options selected by orchestration routing.
/// </summary>
public sealed class OrchestrationTriggerRoutingResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationTriggerRoutingResult"/> class.
    /// </summary>
    /// <param name="matched">A value indicating whether a route matched.</param>
    /// <param name="payload">Payload selected by the matched route.</param>
    /// <param name="options">Operation options selected by the matched route.</param>
    public OrchestrationTriggerRoutingResult(bool matched, object payload, OrchestrationOperationOptions options)
    {
        Matched = matched;
        Payload = payload;
        Options = options ?? new OrchestrationOperationOptions();
    }

    /// <summary>
    /// Gets a value indicating whether a route matched.
    /// </summary>
    public bool Matched { get; }

    /// <summary>
    /// Gets the business payload selected by the route.
    /// </summary>
    public object Payload { get; }

    /// <summary>
    /// Gets the operation options selected by the route.
    /// </summary>
    public OrchestrationOperationOptions Options { get; }

    /// <summary>
    /// Creates an unmatched routing result.
    /// </summary>
    /// <returns>An unmatched routing result.</returns>
    public static OrchestrationTriggerRoutingResult NoMatch()
        => new(false, null, new OrchestrationOperationOptions());
}
