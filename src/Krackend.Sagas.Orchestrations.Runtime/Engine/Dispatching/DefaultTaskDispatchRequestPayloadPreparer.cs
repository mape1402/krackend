namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Transformations;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;
using System.Text.Json.Nodes;

/// <summary>
/// Prepares task dispatch request payloads using the configured runtime transform and validation adapters.
/// </summary>
public sealed class DefaultTaskDispatchRequestPayloadPreparer : ITaskDispatchRequestPayloadPreparer
{
    private readonly IOrchestrationPayloadContextFactory _payloadContextFactory;
    private readonly IOrchestrationTransformationExecutor _transformationExecutor;
    private readonly IOrchestrationValidationExecutor _validationExecutor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTaskDispatchRequestPayloadPreparer"/> class.
    /// </summary>
    /// <param name="payloadContextFactory">Payload context factory.</param>
    /// <param name="transformationExecutor">Transformation executor.</param>
    /// <param name="validationExecutor">Validation executor.</param>
    public DefaultTaskDispatchRequestPayloadPreparer(
        IOrchestrationPayloadContextFactory payloadContextFactory,
        IOrchestrationTransformationExecutor transformationExecutor,
        IOrchestrationValidationExecutor validationExecutor)
    {
        _payloadContextFactory = payloadContextFactory ?? throw new ArgumentNullException(nameof(payloadContextFactory));
        _transformationExecutor = transformationExecutor ?? throw new ArgumentNullException(nameof(transformationExecutor));
        _validationExecutor = validationExecutor ?? throw new ArgumentNullException(nameof(validationExecutor));
    }

    /// <inheritdoc />
    public async Task<TaskDispatchRequestPayloadPreparationResult> PrepareAsync(
        TaskDispatchRequestPayloadPreparationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Instance);
        ArgumentNullException.ThrowIfNull(request.Task);
        ArgumentNullException.ThrowIfNull(request.MessagingConfiguration);

        var payloadContext = _payloadContextFactory.Create(request.Instance, request.StageKey, request.Task.Key);
        var requestPayload = string.IsNullOrWhiteSpace(request.Payload)
            ? payloadContext.TriggerPayload?.DeepClone()
            : JsonNode.Parse(request.Payload);

        if (request.Task.Transformation?.IsEnabled == true)
        {
            var transformResult = await _transformationExecutor.TransformAsync(
                new OrchestrationTransformationRequest
                {
                    Task = request.Task,
                    PayloadContext = payloadContext
                },
                cancellationToken);

            if (!transformResult.Succeeded)
            {
                throw new TaskDispatchPreparationException(
                    string.IsNullOrWhiteSpace(transformResult.ErrorCode) ? "TransformationFailed" : transformResult.ErrorCode,
                    string.IsNullOrWhiteSpace(transformResult.ErrorMessage) ? "Task transformation failed." : transformResult.ErrorMessage,
                    transformResult.Diagnostics);
            }

            requestPayload = transformResult.Payload?.DeepClone();
        }

        var validationBinding = GetRequestValidationBinding(request.MessagingConfiguration);
        var validation = request.MessagingConfiguration.RequestValidation;
        if (ShouldValidate(validationBinding, validation))
        {
            var validationResult = await _validationExecutor.ValidateAsync(
                new OrchestrationValidationRequest
                {
                    Task = request.Task,
                    SchemaBinding = validationBinding,
                    Payload = requestPayload?.DeepClone(),
                    ValidationDsl = GetValidationDsl(validation),
                    Phase = "Request"
                },
                cancellationToken);

            if (!validationResult.Succeeded)
            {
                throw new TaskDispatchPreparationException(
                    GetValidationErrorCode(validationResult.ErrorCode, validation, "RequestValidationFailed"),
                    string.IsNullOrWhiteSpace(validationResult.ErrorMessage) ? "Task request validation failed." : validationResult.ErrorMessage,
                    validationResult.Diagnostics);
            }
        }

        return new TaskDispatchRequestPayloadPreparationResult
        {
            Payload = requestPayload
        };
    }

    private static SchemaBindingArtifact GetRequestValidationBinding(MessagingTaskConfigurationArtifact configuration)
        => configuration.RequestSchemaBinding ?? configuration.SchemaBinding;

    private static bool ShouldValidate(SchemaBindingArtifact schemaBinding, ValidationArtifact validation)
        => schemaBinding?.IsValidationEnabled == true || validation?.IsEnabled == true;

    private static string GetValidationDsl(ValidationArtifact validation)
        => validation?.Configuration is DslValidationConfigurationArtifact dsl ? dsl.Dsl : string.Empty;

    private static string GetValidationErrorCode(
        string adapterErrorCode,
        ValidationArtifact validation,
        string fallbackErrorCode)
    {
        if (!string.IsNullOrWhiteSpace(adapterErrorCode))
        {
            return adapterErrorCode;
        }

        return string.IsNullOrWhiteSpace(validation?.ErrorCode) ? fallbackErrorCode : validation.ErrorCode;
    }
}
