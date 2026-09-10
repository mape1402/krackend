namespace Krackend.Sagas.Orchestrations.Runtime.ButterMorph;

using System.Text.Json.Nodes;
using global::ButterMorph.Abstractions;
using global::ButterMorph.Core;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Transformations;

/// <summary>
/// Executes orchestration task transformations with ButterMorph DSL.
/// </summary>
public sealed class ButterMorphOrchestrationTransformationExecutor : IOrchestrationTransformationExecutor
{
    private readonly IButterMorphEngine _engine;
    private readonly IDslParser _dslParser;
    private readonly IButterMorphDiagnosticMetadataMapper _diagnosticMapper;
    private readonly IButterMorphSourceGraphBuilder _sourceGraphBuilder;
    private readonly global::ButterMorph.Json.JsonWriter _jsonWriter = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ButterMorphOrchestrationTransformationExecutor"/> class.
    /// </summary>
    /// <param name="engine">ButterMorph execution engine.</param>
    /// <param name="dslParser">ButterMorph DSL parser.</param>
    /// <param name="diagnosticMapper">Maps ButterMorph diagnostics into orchestration metadata.</param>
    /// <param name="sourceGraphBuilder">Builds ButterMorph source graphs from the orchestration payload context.</param>
    public ButterMorphOrchestrationTransformationExecutor(
        IButterMorphEngine engine,
        IDslParser dslParser,
        IButterMorphDiagnosticMetadataMapper diagnosticMapper,
        IButterMorphSourceGraphBuilder sourceGraphBuilder)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _dslParser = dslParser ?? throw new ArgumentNullException(nameof(dslParser));
        _diagnosticMapper = diagnosticMapper ?? throw new ArgumentNullException(nameof(diagnosticMapper));
        _sourceGraphBuilder = sourceGraphBuilder ?? throw new ArgumentNullException(nameof(sourceGraphBuilder));
    }

    /// <inheritdoc />
    public Task<OrchestrationTransformationResult> TransformAsync(
        OrchestrationTransformationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        if (request.Task.Transformation?.Configuration is not DslTransformationConfigurationArtifact configuration ||
            string.IsNullOrWhiteSpace(configuration.Dsl))
        {
            return Task.FromResult(OrchestrationTransformationResult.Failure(
                "TransformationDslMissing",
                $"Task '{request.Task.Key}' has transformation enabled but no ButterMorph DSL was provided."));
        }

        try
        {
            var document = _dslParser.Parse(new DslDefinition { Content = configuration.Dsl });
            var sources = _sourceGraphBuilder.Build(request.PayloadContext);
            var result = _engine.Transform(new TransformationRequest
            {
                Sources = sources,
                Definition = document
            });

            if (!result.Succeeded)
            {
                return Task.FromResult(OrchestrationTransformationResult.Failure(
                    "TransformationFailed",
                    $"Task '{request.Task.Key}' transformation failed.",
                    _diagnosticMapper.Map(result.Diagnostics)));
            }

            var output = _jsonWriter.Write(result.ResultGraph);
            var payload = string.IsNullOrWhiteSpace(output.Content)
                ? null
                : JsonNode.Parse(output.Content);

            return Task.FromResult(OrchestrationTransformationResult.Success(payload));
        }
        catch (Exception exception)
        {
            return Task.FromResult(OrchestrationTransformationResult.Failure(
                "TransformationExecutionFailed",
                exception.Message,
                _diagnosticMapper.Map(exception)));
        }
    }
}
