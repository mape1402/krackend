using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Default timeout policy evaluator.
/// </summary>
public sealed class RuntimeTimeoutPolicyEvaluator : IRuntimeTimeoutPolicyEvaluator
{
    /// <inheritdoc/>
    public RuntimeTimeoutPolicy Evaluate(JsonObject timeoutPolicy)
    {
        if (timeoutPolicy is null || timeoutPolicy.Count == 0)
            return new RuntimeTimeoutPolicy();

        var behavior = ReadEnumName<TimeoutBehavior>(timeoutPolicy, TimeoutBehavior.Fail, "TimeoutBehavior", "timeoutBehavior", "Behavior", "behavior");
        var policy = ReadObject(timeoutPolicy, "TimeoutBehaviorPolicy", "timeoutBehaviorPolicy", "Policy", "policy");
        return new RuntimeTimeoutPolicy
        {
            Timeout = ReadDuration(timeoutPolicy["Timeout"] ?? timeoutPolicy["timeout"]),
            Behavior = behavior,
            OrchestrationAction = policy is null
                ? string.Empty
                : ReadEnumName<OrchestrationActionOnTimeout>(policy, OrchestrationActionOnTimeout.Block, "OrchestrationAction", "orchestrationAction"),
            WaitingTime = policy is null
                ? TimeSpan.Zero
                : ReadDuration(policy["WaitingTime"] ?? policy["waitingTime"]),
            ErrorCode = policy is null
                ? string.Empty
                : ReadString(policy, "ErrorCode", "errorCode"),
            ReconcileRetryPolicy = policy is null
                ? new RuntimeRetryPolicy()
                : ReadRetryPolicy(ReadObject(policy, "RetryPolicy", "retryPolicy"))
        };
    }

    private static RuntimeRetryPolicy ReadRetryPolicy(JsonObject retryPolicy)
    {
        if (retryPolicy is null || retryPolicy.Count == 0)
            return new RuntimeRetryPolicy();

        return new RuntimeRetryPolicy
        {
            MaxRetries = Math.Max(0, ReadInt(retryPolicy, "MaxRetries", "maxRetries")),
            StrategyType = ReadString(retryPolicy, "StrategyType", "strategyType")
        };
    }

    private static JsonObject ReadObject(JsonObject obj, params string[] names)
        => names.Select(name => obj[name]).OfType<JsonObject>().FirstOrDefault();

    private static string ReadString(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj[name] is JsonValue value && value.TryGetValue<string>(out var text))
                return text ?? string.Empty;
        }

        return string.Empty;
    }

    private static int ReadInt(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj[name] is JsonValue value && value.TryGetValue<int>(out var result))
                return result;
        }

        return 0;
    }

    private static TimeSpan ReadDuration(JsonNode node)
    {
        if (node is null)
            return TimeSpan.Zero;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<double>(out var seconds))
                return TimeSpan.FromSeconds(seconds);

            if (value.TryGetValue<string>(out var text))
                return TimeSpan.TryParse(text, out var parsed) ? parsed : TimeSpan.Zero;
        }

        if (node is JsonObject obj)
        {
            if (obj["Value"] is JsonValue valueNode)
                return ReadDuration(valueNode);

            if (TryReadDouble(obj, out var totalSeconds, "TotalSeconds", "totalSeconds", "Seconds", "seconds"))
                return TimeSpan.FromSeconds(totalSeconds);

            if (TryReadDouble(obj, out var ticks, "Ticks", "ticks"))
                return TimeSpan.FromTicks((long)ticks);
        }

        return TimeSpan.Zero;
    }

    private static bool TryReadDouble(JsonObject obj, out double result, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj[name] is JsonValue value && value.TryGetValue<double>(out result))
                return true;
        }

        result = 0;
        return false;
    }

    private static string ReadEnumName<TEnum>(JsonObject obj, TEnum defaultValue, params string[] names)
        where TEnum : struct, Enum
    {
        foreach (var name in names)
        {
            var node = obj[name];
            if (node is JsonValue value)
            {
                if (value.TryGetValue<string>(out var text)
                    && Enum.TryParse<TEnum>(text, true, out var parsedText))
                    return parsedText.ToString();

                if (value.TryGetValue<int>(out var number)
                    && Enum.IsDefined(typeof(TEnum), number))
                    return ((TEnum)Enum.ToObject(typeof(TEnum), number)).ToString();
            }
        }

        return defaultValue.ToString();
    }
}
