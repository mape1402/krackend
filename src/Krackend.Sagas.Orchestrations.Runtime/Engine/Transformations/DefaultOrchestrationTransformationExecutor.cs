namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Transformations;

/// <summary>
/// Provides pass-through transformation behavior when no transform adapter is configured.
/// </summary>
public sealed class DefaultOrchestrationTransformationExecutor : IOrchestrationTransformationExecutor
{
    /// <inheritdoc />
    public Task<OrchestrationTransformationResult> TransformAsync(
        OrchestrationTransformationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(OrchestrationTransformationResult.Failure(
            "TransformationAdapterNotConfigured",
            "Task transformation is enabled, but no transformation adapter is configured."));
    }
}
