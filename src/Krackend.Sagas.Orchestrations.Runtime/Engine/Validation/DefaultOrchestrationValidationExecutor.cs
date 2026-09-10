namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;

/// <summary>
/// Provides successful validation behavior when no validation adapter is configured.
/// </summary>
public sealed class DefaultOrchestrationValidationExecutor : IOrchestrationValidationExecutor
{
    /// <inheritdoc />
    public Task<OrchestrationValidationResult> ValidateAsync(
        OrchestrationValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(OrchestrationValidationResult.Failure(
            "ValidationAdapterNotConfigured",
            "Payload validation is enabled, but no validation adapter is configured."));
    }
}
