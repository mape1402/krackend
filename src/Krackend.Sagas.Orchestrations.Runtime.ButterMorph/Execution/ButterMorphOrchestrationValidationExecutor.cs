namespace Krackend.Sagas.Orchestrations.Runtime.ButterMorph;

using global::ButterMorph.Abstractions;
using global::ButterMorph.Core;
using global::ButterMorph.Json;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;

/// <summary>
/// Executes orchestration validation rules with ButterMorph DSL.
/// </summary>
public sealed class ButterMorphOrchestrationValidationExecutor : IOrchestrationValidationExecutor
{
    private readonly IButterMorphEngine _engine;
    private readonly IDslParser _dslParser;
    private readonly IButterMorphDiagnosticMetadataMapper _diagnosticMapper;
    private readonly JsonReader _jsonReader = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ButterMorphOrchestrationValidationExecutor"/> class.
    /// </summary>
    /// <param name="engine">ButterMorph execution engine.</param>
    /// <param name="dslParser">ButterMorph DSL parser.</param>
    /// <param name="diagnosticMapper">Maps ButterMorph diagnostics into orchestration metadata.</param>
    public ButterMorphOrchestrationValidationExecutor(
        IButterMorphEngine engine,
        IDslParser dslParser,
        IButterMorphDiagnosticMetadataMapper diagnosticMapper)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _dslParser = dslParser ?? throw new ArgumentNullException(nameof(dslParser));
        _diagnosticMapper = diagnosticMapper ?? throw new ArgumentNullException(nameof(diagnosticMapper));
    }

    /// <inheritdoc />
    public Task<OrchestrationValidationResult> ValidateAsync(
        OrchestrationValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.ValidationDsl))
        {
            if (request.SchemaBinding?.IsValidationEnabled == true)
            {
                return Task.FromResult(OrchestrationValidationResult.Failure(
                    $"{request.Phase}SchemaValidationNotConfigured",
                    $"{request.Phase} schema validation is enabled, but the artifact does not contain executable ButterMorph validation DSL."));
            }

            return Task.FromResult(OrchestrationValidationResult.Success());
        }

        try
        {
            var document = _dslParser.Parse(new DslDefinition { Content = request.ValidationDsl });
            var source = _jsonReader.Read(new StructureInput
            {
                Format = "json",
                Content = request.Payload?.ToJsonString() ?? "{}"
            });
            var result = _engine.Validate(new ValidationRequest
            {
                SourceGraph = source,
                Definition = document
            });

            return Task.FromResult(result.IsValid
                ? OrchestrationValidationResult.Success()
                : OrchestrationValidationResult.Failure(
                    $"{request.Phase}ValidationFailed",
                    $"{request.Phase} payload validation failed.",
                    _diagnosticMapper.Map(result.Diagnostics)));
        }
        catch (Exception exception)
        {
            return Task.FromResult(OrchestrationValidationResult.Failure(
                $"{request.Phase}ValidationExecutionFailed",
                exception.Message,
                _diagnosticMapper.Map(exception)));
        }
    }
}
