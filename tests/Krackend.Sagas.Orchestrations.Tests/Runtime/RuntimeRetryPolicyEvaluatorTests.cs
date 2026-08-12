using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeRetryPolicyEvaluatorTests
{
    [Fact]
    public void Evaluate_WhenPolicyIsEmpty_ReturnsSingleAttempt()
    {
        var evaluator = new RuntimeRetryPolicyEvaluator();

        var policy = evaluator.Evaluate(new JsonObject());

        Assert.Equal(0, policy.MaxRetries);
        Assert.Equal(1, policy.MaxAttempts);
    }

    [Fact]
    public void Evaluate_WhenMaxRetriesIsConfigured_ReturnsTotalAttempts()
    {
        var evaluator = new RuntimeRetryPolicyEvaluator();
        var retryPolicy = JsonNode.Parse("""{"MaxRetries":3,"StrategyType":0}""")!.AsObject();

        var policy = evaluator.Evaluate(retryPolicy);

        Assert.Equal(3, policy.MaxRetries);
        Assert.Equal(4, policy.MaxAttempts);
        Assert.Equal("Fixed", policy.StrategyType);
    }
}
