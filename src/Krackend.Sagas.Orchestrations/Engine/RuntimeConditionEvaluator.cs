using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Minimal runtime condition evaluator for promoted DSL conditions.
/// </summary>
public sealed class RuntimeConditionEvaluator : IRuntimeConditionEvaluator
{
    /// <inheritdoc/>
    public RuntimeConditionEvaluation Evaluate(JsonObject condition, JsonNode payload)
    {
        if (condition is null || condition.Count == 0)
            return RuntimeConditionEvaluation.Execute();

        var expression = ReadExpression(condition);
        if (string.IsNullOrWhiteSpace(expression))
            return RuntimeConditionEvaluation.Execute();

        if (string.Equals(expression, "true", StringComparison.OrdinalIgnoreCase))
            return RuntimeConditionEvaluation.Execute(expression);

        if (string.Equals(expression, "false", StringComparison.OrdinalIgnoreCase))
            return RuntimeConditionEvaluation.Skip(expression, "Execution condition evaluated to false.");

        throw new NotSupportedException($"Execution condition expression '{expression}' is not supported by the runtime DSL evaluator yet.");
    }

    private static string ReadExpression(JsonObject condition)
    {
        var configuration = ReadObject(condition, "Configuration", "configuration");
        if (configuration is null)
            return string.Empty;

        var expression = configuration["Expression"] ?? configuration["expression"];
        if (expression is null)
            return string.Empty;

        if (expression is JsonValue value && value.TryGetValue<string>(out var text))
            return text ?? string.Empty;

        if (expression is JsonObject expressionObject)
        {
            var nested = expressionObject["Value"] ?? expressionObject["value"];
            if (nested is JsonValue nestedValue && nestedValue.TryGetValue<string>(out var nestedText))
                return nestedText ?? string.Empty;
        }

        return expression.ToString();
    }

    private static JsonObject ReadObject(JsonObject obj, params string[] names)
        => names.Select(name => obj[name]).OfType<JsonObject>().FirstOrDefault();
}
