namespace Krackend.Sagas.Orchestrations.Client.Errors;

/// <summary>
/// Resolves the orchestration error code that represents a client-side exception.
/// </summary>
public interface IOrchestrationExceptionErrorCodeMapper
{
    /// <summary>
    /// Resolves the configured orchestration error code for an exception.
    /// </summary>
    /// <param name="exception">Exception raised by the business operation.</param>
    /// <returns>The error code sent to the orchestrator as execution metadata.</returns>
    string Resolve(Exception exception);
}
