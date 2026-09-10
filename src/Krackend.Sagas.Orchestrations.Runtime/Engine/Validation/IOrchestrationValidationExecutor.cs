namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;

/// <summary>
/// Executes configured runtime payload validations.
/// </summary>
public interface IOrchestrationValidationExecutor
{
    /// <summary>
    /// Validates a business payload against configured orchestration validation rules.
    /// </summary>
    /// <param name="request">Validation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    Task<OrchestrationValidationResult> ValidateAsync(
        OrchestrationValidationRequest request,
        CancellationToken cancellationToken = default);
}
