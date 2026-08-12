using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Reads runtime retry policy configuration.
/// </summary>
public sealed class RuntimeRetryPolicyEvaluator : IRuntimeRetryPolicyEvaluator
{
    /// <inheritdoc/>
    public RuntimeRetryPolicy Evaluate(JsonObject retryPolicy)
    {
        if (retryPolicy is null || retryPolicy.Count == 0)
            return new RuntimeRetryPolicy();

        return new RuntimeRetryPolicy
        {
            MaxRetries = Math.Max(0, ReadInt(retryPolicy, "MaxRetries", "maxRetries")),
            StrategyType = ReadString(retryPolicy, "StrategyType", "strategyType")
        };
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

    private static string ReadString(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            var node = obj[name];
            if (node is null)
                continue;

            if (node is JsonValue value)
            {
                if (value.TryGetValue<string>(out var text))
                    return text ?? string.Empty;

                if (value.TryGetValue<int>(out var number))
                    return number == 0 ? "Fixed" : number.ToString();
            }
        }

        return "Fixed";
    }
}
