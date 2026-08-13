using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Evaluates stage-level branch rules from promoted runtime artifacts.
/// </summary>
internal sealed class RuntimeBranchRuleEvaluator : IRuntimeBranchRuleEvaluator
{
    private readonly IRuntimeConditionEvaluator _conditionEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeBranchRuleEvaluator"/> class.
    /// </summary>
    public RuntimeBranchRuleEvaluator(IRuntimeConditionEvaluator conditionEvaluator)
    {
        _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
    }

    /// <inheritdoc/>
    public RuntimeBranchDecision EvaluateStage(RuntimeArtifactDocument document, RuntimeStageDocument stage, JsonNode payload)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));

        if (stage is null)
            throw new ArgumentNullException(nameof(stage));

        if (stage.BranchRules.Count == 0)
            return RuntimeBranchDecision.NoRules;

        var stages = document.Stages.ToArray();
        foreach (var rule in stage.BranchRules)
        {
            if (!IsStageScopedRule(rule, stage))
                continue;

            var ruleId = ReadString(rule, "Id", "id");
            var condition = ReadObject(rule, "Condition", "condition");
            var conditionResult = _conditionEvaluator.Evaluate(condition, payload);
            if (!conditionResult.ShouldExecute)
                continue;

            if (!IsStageNavigation(rule))
                return RuntimeBranchDecision.Unsupported(ruleId, "Only stage-to-stage branch navigation is supported by the runtime.");

            var navigateToId = ReadString(rule, "NavigateToId", "navigateToId");
            var targetIndex = Array.FindIndex(stages, x => string.Equals(x.Id, navigateToId, StringComparison.OrdinalIgnoreCase));
            if (targetIndex < 0)
                return RuntimeBranchDecision.Unsupported(ruleId, $"Branch target stage '{navigateToId}' was not found in the runtime artifact.");

            return RuntimeBranchDecision.Taken(ruleId, stages[targetIndex].Key, targetIndex);
        }

        return RuntimeBranchDecision.NotTaken("No branch rule matched.");
    }

    private static bool IsStageScopedRule(JsonObject rule, RuntimeStageDocument stage)
    {
        if (!string.Equals(ReadEnumName<ElementType>(rule, ElementType.Stage, "FromType", "fromType"), nameof(ElementType.Stage), StringComparison.OrdinalIgnoreCase))
            return false;

        var fromId = ReadString(rule, "FromId", "fromId");
        return string.IsNullOrWhiteSpace(fromId) || string.Equals(fromId, stage.Id, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStageNavigation(JsonObject rule)
        => string.Equals(ReadEnumName<ElementType>(rule, ElementType.Stage, "NavigateToType", "navigateToType"), nameof(ElementType.Stage), StringComparison.OrdinalIgnoreCase);

    private static JsonObject ReadObject(JsonObject obj, params string[] names)
        => names.Select(name => obj[name]).OfType<JsonObject>().FirstOrDefault() ?? new JsonObject();

    private static string ReadString(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            var node = obj[name];
            if (node is JsonValue value && value.TryGetValue<string>(out var text))
                return text ?? string.Empty;

            if (node is JsonObject nested && nested["Value"] is JsonValue nestedValue && nestedValue.TryGetValue<string>(out var nestedText))
                return nestedText ?? string.Empty;
        }

        return string.Empty;
    }

    private static string ReadEnumName<TEnum>(JsonObject obj, TEnum defaultValue, params string[] names)
        where TEnum : struct, Enum
    {
        foreach (var name in names)
        {
            var node = obj[name];
            if (node is not JsonValue value)
                continue;

            if (value.TryGetValue<string>(out var text)
                && Enum.TryParse<TEnum>(text, true, out var parsedText))
                return parsedText.ToString();

            if (value.TryGetValue<int>(out var number)
                && Enum.IsDefined(typeof(TEnum), number))
                return ((TEnum)Enum.ToObject(typeof(TEnum), number)).ToString();
        }

        return defaultValue.ToString();
    }
}
