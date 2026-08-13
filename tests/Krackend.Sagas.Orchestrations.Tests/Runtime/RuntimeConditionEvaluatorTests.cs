using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeConditionEvaluatorTests
{
    [Fact]
    public void Evaluate_WhenConditionIsEmpty_AllowsExecution()
    {
        var evaluator = new RuntimeConditionEvaluator();

        var result = evaluator.Evaluate(new JsonObject(), JsonNode.Parse("""{"id":1}"""));

        Assert.True(result.ShouldExecute);
    }

    [Fact]
    public void Evaluate_WhenExpressionIsFalse_SkipsExecution()
    {
        var evaluator = new RuntimeConditionEvaluator();
        var condition = JsonNode.Parse("""
        {
          "Configuration": {
            "Expression": "false"
          }
        }
        """)!.AsObject();

        var result = evaluator.Evaluate(condition, JsonNode.Parse("""{"id":1}"""));

        Assert.False(result.ShouldExecute);
        Assert.Contains("false", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_WhenConditionIsDisabled_AllowsExecutionWithoutEvaluatingExpression()
    {
        var evaluator = new RuntimeConditionEvaluator();
        var condition = JsonNode.Parse("""
        {
          "IsEnabled": false,
          "Configuration": {
            "Expression": "payload.total > 0"
          }
        }
        """)!.AsObject();

        var result = evaluator.Evaluate(condition, JsonNode.Parse("""{"total":0}"""));

        Assert.True(result.ShouldExecute);
    }

    [Fact]
    public void Evaluate_WhenExpressionIsUnsupported_FailsExplicitly()
    {
        var evaluator = new RuntimeConditionEvaluator();
        var condition = JsonNode.Parse("""
        {
          "Configuration": {
            "Expression": "payload.total > 0"
          }
        }
        """)!.AsObject();

        var ex = Assert.Throws<NotSupportedException>(() => evaluator.Evaluate(condition, JsonNode.Parse("""{"total":10}""")));

        Assert.Contains("not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
