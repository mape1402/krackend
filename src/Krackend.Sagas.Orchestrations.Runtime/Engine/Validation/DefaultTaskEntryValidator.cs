namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;

/// <summary>
/// Default task entry validator that delegates executable rules to the configured validation adapter.
/// </summary>
public sealed class DefaultTaskEntryValidator : ITaskEntryValidator
{
    private const string DefaultErrorCode = "TaskEntryValidationFailed";

    private readonly IOrchestrationValidationExecutor _validationExecutor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTaskEntryValidator"/> class.
    /// </summary>
    /// <param name="validationExecutor">Validation executor.</param>
    public DefaultTaskEntryValidator(IOrchestrationValidationExecutor validationExecutor)
    {
        _validationExecutor = validationExecutor ?? throw new ArgumentNullException(nameof(validationExecutor));
    }

    /// <inheritdoc />
    public async Task<OrchestrationValidationResult> ValidateAsync(
        TaskArtifact task,
        OrchestrationPayloadContext payloadContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        var validation = task.EntryValidation;
        if (validation?.IsEnabled != true)
        {
            return OrchestrationValidationResult.Success();
        }

        if (validation.Configuration is not DslValidationConfigurationArtifact configuration ||
            string.IsNullOrWhiteSpace(configuration.Dsl))
        {
            return OrchestrationValidationResult.Failure(
                GetErrorCode(validation),
                $"Task '{task.Key}' has entry validation enabled but no executable ButterMorph DSL was provided.");
        }

        var result = await _validationExecutor.ValidateAsync(
            new OrchestrationValidationRequest
            {
                Task = task,
                PayloadContext = payloadContext,
                PayloadAlias = "context",
                ValidationDsl = configuration.Dsl,
                Phase = "TaskEntry"
            },
            cancellationToken);

        return result.Succeeded
            ? result
            : OrchestrationValidationResult.Failure(
                GetErrorCode(validation, result.ErrorCode),
                string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? $"Task '{task.Key}' entry validation failed."
                    : result.ErrorMessage,
                result.Diagnostics);
    }

    private static string GetErrorCode(ValidationArtifact validation, string adapterErrorCode = null)
    {
        if (!string.IsNullOrWhiteSpace(adapterErrorCode))
        {
            return adapterErrorCode;
        }

        return string.IsNullOrWhiteSpace(validation?.ErrorCode) ? DefaultErrorCode : validation.ErrorCode;
    }
}
