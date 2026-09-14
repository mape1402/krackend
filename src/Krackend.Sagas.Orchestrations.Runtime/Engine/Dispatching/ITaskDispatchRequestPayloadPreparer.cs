namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

/// <summary>
/// Prepares the business request payload that will be sent by a task dispatch.
/// </summary>
public interface ITaskDispatchRequestPayloadPreparer
{
    /// <summary>
    /// Applies runtime transform and validation rules to the dispatch request payload.
    /// </summary>
    /// <param name="request">Payload preparation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The prepared request payload.</returns>
    Task<TaskDispatchRequestPayloadPreparationResult> PrepareAsync(
        TaskDispatchRequestPayloadPreparationRequest request,
        CancellationToken cancellationToken = default);
}
