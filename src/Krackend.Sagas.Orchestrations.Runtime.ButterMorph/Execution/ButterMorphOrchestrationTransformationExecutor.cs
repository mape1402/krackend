namespace Krackend.Sagas.Orchestrations.Runtime.ButterMorph;

using System.Text.Json.Nodes;
using global::ButterMorph.Abstractions;
using global::ButterMorph.Core;
using global::ButterMorph.Json;
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
    private readonly JsonReader _jsonReader = new();
    private readonly JsonWriter _jsonWriter = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ButterMorphOrchestrationTransformationExecutor"/> class.
    /// </summary>
    /// <param name="engine">ButterMorph execution engine.</param>
    /// <param name="dslParser">ButterMorph DSL parser.</param>
    /// <param name="diagnosticMapper">Maps ButterMorph diagnostics into orchestration metadata.</param>
    public ButterMorphOrchestrationTransformationExecutor(
        IButterMorphEngine engine,
        IDslParser dslParser,
        IButterMorphDiagnosticMetadataMapper diagnosticMapper)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _dslParser = dslParser ?? throw new ArgumentNullException(nameof(dslParser));
        _diagnosticMapper = diagnosticMapper ?? throw new ArgumentNullException(nameof(diagnosticMapper));
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
            var sources = BuildSources(request);
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

    private IReadOnlyDictionary<string, IStructureGraph> BuildSources(OrchestrationTransformationRequest request)
    {
        var sources = new Dictionary<string, IStructureGraph>(StringComparer.OrdinalIgnoreCase);
        AddSource(sources, "context", request.PayloadContext.ContextPayload);
        AddSource(sources, "trigger", request.PayloadContext.TriggerPayload);
        return sources;
    }

    private void AddSource(
        IDictionary<string, IStructureGraph> sources,
        string alias,
        JsonNode payload)
    {
        if (payload is null)
        {
            return;
        }

        var graph = _jsonReader.Read(new StructureInput
        {
            Format = "json",
            Content = payload.ToJsonString()
        });

        sources[alias] = graph;
    }
}
