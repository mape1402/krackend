namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Promotion;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;
using System.Text.Json.Nodes;

/// <summary>
/// Validates trigger payloads using validation metadata carried by the orchestration artifact.
/// </summary>
internal sealed class DefaultTriggerPayloadValidator : ITriggerPayloadValidator
{
    private readonly IOrchestrationValidationExecutor _validationExecutor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTriggerPayloadValidator"/> class.
    /// </summary>
    /// <param name="validationExecutor">Validation executor.</param>
    public DefaultTriggerPayloadValidator(IOrchestrationValidationExecutor validationExecutor)
    {
        _validationExecutor = validationExecutor ?? throw new ArgumentNullException(nameof(validationExecutor));
    }

    /// <inheritdoc />
    public async Task<OrchestrationValidationResult> ValidateAsync(
        ResolvedOrchestrationArtifact artifact,
        JsonNode payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var trigger = artifact.Artifact.TriggerBindings
            .FirstOrDefault(x => x.IsEnabled && x.TriggerChannel is EventTriggerChannelArtifact);
        if (trigger?.TriggerChannel is not EventTriggerChannelArtifact eventTrigger)
        {
            return OrchestrationValidationResult.Success();
        }

        var validation = eventTrigger.Validation;
        var schemaBinding = eventTrigger.SchemaBinding;
        if (schemaBinding?.IsValidationEnabled != true && validation?.IsEnabled != true)
        {
            return OrchestrationValidationResult.Success();
        }

        var validationResult = await _validationExecutor.ValidateAsync(
            new OrchestrationValidationRequest
            {
                Trigger = trigger,
                SchemaBinding = schemaBinding,
                Payload = payload?.DeepClone(),
                ValidationDsl = GetValidationDsl(validation),
                Phase = "Trigger"
            },
            cancellationToken);

        if (validationResult.Succeeded)
        {
            return validationResult;
        }

        var errorCode = string.IsNullOrWhiteSpace(validationResult.ErrorCode)
            ? validation?.ErrorCode
            : validationResult.ErrorCode;

        return OrchestrationValidationResult.Failure(
            string.IsNullOrWhiteSpace(errorCode) ? "TriggerValidationFailed" : errorCode,
            string.IsNullOrWhiteSpace(validationResult.ErrorMessage)
                ? "Trigger payload validation failed."
                : validationResult.ErrorMessage,
            validationResult.Diagnostics);
    }

    private static string GetValidationDsl(ValidationArtifact validation)
        => validation?.Configuration is DslValidationConfigurationArtifact dsl ? dsl.Dsl : string.Empty;
}
