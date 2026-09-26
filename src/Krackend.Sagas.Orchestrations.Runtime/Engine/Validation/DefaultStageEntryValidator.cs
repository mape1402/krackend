namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;

/// <summary>
/// Default stage entry validator that delegates executable rules to the configured validation adapter.
/// </summary>
public sealed class DefaultStageEntryValidator : IStageEntryValidator
{
    private const string DefaultErrorCode = "StageEntryValidationFailed";

    private readonly IOrchestrationValidationExecutor _validationExecutor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultStageEntryValidator"/> class.
    /// </summary>
    /// <param name="validationExecutor">Validation executor.</param>
    public DefaultStageEntryValidator(IOrchestrationValidationExecutor validationExecutor)
    {
        _validationExecutor = validationExecutor ?? throw new ArgumentNullException(nameof(validationExecutor));
    }

    /// <inheritdoc />
    public async Task<OrchestrationValidationResult> ValidateAsync(
        StageArtifact stage,
        OrchestrationPayloadContext payloadContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stage);

        var validation = stage.EntryValidation;
        if (validation?.IsEnabled != true)
        {
            return OrchestrationValidationResult.Success();
        }

        if (validation.Configuration is not DslValidationConfigurationArtifact configuration ||
            string.IsNullOrWhiteSpace(configuration.Dsl))
        {
            return OrchestrationValidationResult.Failure(
                GetErrorCode(validation),
                $"Stage '{stage.Key}' has entry validation enabled but no executable ButterMorph DSL was provided.");
        }

        var result = await _validationExecutor.ValidateAsync(
            new OrchestrationValidationRequest
            {
                PayloadContext = payloadContext,
                PayloadAlias = "context",
                ValidationDsl = configuration.Dsl,
                Phase = "StageEntry"
            },
            cancellationToken);

        return result.Succeeded
            ? result
            : OrchestrationValidationResult.Failure(
                GetErrorCode(validation, result.ErrorCode),
                string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? $"Stage '{stage.Key}' entry validation failed."
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
