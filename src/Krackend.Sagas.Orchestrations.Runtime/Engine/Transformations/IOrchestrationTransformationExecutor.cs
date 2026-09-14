namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Transformations;

/// <summary>
/// Executes configured task transformations at runtime.
/// </summary>
public interface IOrchestrationTransformationExecutor
{
    /// <summary>
    /// Executes the transformation configured for a task.
    /// </summary>
    /// <param name="request">Transformation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transformation result.</returns>
    Task<OrchestrationTransformationResult> TransformAsync(
        OrchestrationTransformationRequest request,
        CancellationToken cancellationToken = default);
}
