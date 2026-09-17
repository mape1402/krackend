namespace Krackend.Sagas.Orchestrations.Runtime.ButterMorph;

using System.Text.Json.Nodes;
using global::ButterMorph.Abstractions;
using global::ButterMorph.Core;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Conditions;

/// <summary>
/// Evaluates orchestration execution conditions with ButterMorph DSL.
/// </summary>
public sealed class ButterMorphOrchestrationConditionEvaluator : IOrchestrationConditionEvaluator
{
    private const string ResultPropertyName = "Result";

    private readonly IButterMorphEngine _engine;
    private readonly IDslParser _dslParser;
    private readonly IButterMorphDiagnosticMetadataMapper _diagnosticMapper;
    private readonly IButterMorphSourceGraphBuilder _sourceGraphBuilder;
    private readonly global::ButterMorph.Json.JsonWriter _jsonWriter = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ButterMorphOrchestrationConditionEvaluator"/> class.
    /// </summary>
    /// <param name="engine">ButterMorph execution engine.</param>
    /// <param name="dslParser">ButterMorph DSL parser.</param>
    /// <param name="diagnosticMapper">Maps ButterMorph diagnostics into orchestration metadata.</param>
    /// <param name="sourceGraphBuilder">Builds ButterMorph source graphs from the orchestration payload context.</param>
    public ButterMorphOrchestrationConditionEvaluator(
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
    public Task<OrchestrationConditionEvaluationResult> EvaluateAsync(
        OrchestrationConditionEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        if (request.Condition?.IsEnabled != true)
        {
            return Task.FromResult(OrchestrationConditionEvaluationResult.Success(true));
        }

        if (request.Condition.Configuration is not DslConditionConfigurationArtifact configuration)
        {
            return Task.FromResult(OrchestrationConditionEvaluationResult.Failure(
                "ConditionConfigurationNotSupported",
                $"Condition configuration '{request.Condition.Configuration?.GetType().Name ?? "Unknown"}' is not supported."));
        }

        var expression = configuration.Expression.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Task.FromResult(OrchestrationConditionEvaluationResult.Failure(
                "ConditionExpressionMissing",
                $"The {request.Phase} condition for '{request.ElementKey}' is enabled but does not contain an expression."));
        }

        if (bool.TryParse(expression, out var literal))
        {
            return Task.FromResult(OrchestrationConditionEvaluationResult.Success(literal));
        }

        try
        {
            var document = _dslParser.Parse(new DslDefinition { Content = BuildConditionDsl(expression) });
            var result = _engine.Transform(new TransformationRequest
            {
                Sources = _sourceGraphBuilder.Build(request.PayloadContext),
                Definition = document
            });

            if (!result.Succeeded)
            {
                return Task.FromResult(OrchestrationConditionEvaluationResult.Failure(
                    "ConditionEvaluationFailed",
                    $"The {request.Phase} condition for '{request.ElementKey}' could not be evaluated.",
                    _diagnosticMapper.Map(result.Diagnostics)));
            }

            var output = _jsonWriter.Write(result.ResultGraph);
            var payload = string.IsNullOrWhiteSpace(output.Content)
                ? null
                : JsonNode.Parse(output.Content);

            if (TryReadBoolean(payload?[ResultPropertyName], out var shouldExecute))
            {
                return Task.FromResult(OrchestrationConditionEvaluationResult.Success(shouldExecute));
            }

            return Task.FromResult(OrchestrationConditionEvaluationResult.Failure(
                "ConditionResultNotBoolean",
                $"The {request.Phase} condition for '{request.ElementKey}' did not return a boolean value."));
        }
        catch (Exception exception)
        {
            return Task.FromResult(OrchestrationConditionEvaluationResult.Failure(
                "ConditionExecutionFailed",
                exception.Message,
                _diagnosticMapper.Map(exception)));
        }
    }

    private static string BuildConditionDsl(string expression)
        => $$"""
           target {
             {{ResultPropertyName}}: {{expression}}
           }
           """;

    private static bool TryReadBoolean(JsonNode value, out bool result)
    {
        result = false;
        if (value is null)
        {
            return false;
        }

        try
        {
            if (value.GetValueKind() == System.Text.Json.JsonValueKind.True ||
                value.GetValueKind() == System.Text.Json.JsonValueKind.False)
            {
                result = value.GetValue<bool>();
                return true;
            }

            if (value.GetValueKind() == System.Text.Json.JsonValueKind.String &&
                bool.TryParse(value.GetValue<string>(), out result))
            {
                return true;
            }

            if (value.GetValueKind() == System.Text.Json.JsonValueKind.Number &&
                value.GetValue<decimal>() is var numeric)
            {
                result = numeric != 0;
                return true;
            }
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }

        return false;
    }
}
