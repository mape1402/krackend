using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeTimeoutPolicyEvaluatorTests
{
    [Fact]
    public void Evaluate_WhenPolicyIsEmpty_ReturnsUnconfiguredFailPolicy()
    {
        var policy = new RuntimeTimeoutPolicyEvaluator().Evaluate(new JsonObject());

        Assert.False(policy.IsConfigured);
        Assert.Equal("Fail", policy.Behavior);
    }

    [Fact]
    public void Evaluate_ReadsFailTimeoutPolicy()
    {
        var policy = new RuntimeTimeoutPolicyEvaluator().Evaluate(JsonNode.Parse($$"""
        {
          "Timeout": { "Value": "00:00:30" },
          "TimeoutBehavior": {{(int)TimeoutBehavior.Fail}},
          "TimeoutBehaviorPolicy": { "ErrorCode": "TaskTimeout" }
        }
        """)!.AsObject());

        Assert.True(policy.IsConfigured);
        Assert.Equal(TimeSpan.FromSeconds(30), policy.Timeout);
        Assert.Equal("Fail", policy.Behavior);
        Assert.Equal("TaskTimeout", policy.ErrorCode);
    }

    [Fact]
    public void Evaluate_ReadsWaitPolicyActionAndWaitingTime()
    {
        var policy = new RuntimeTimeoutPolicyEvaluator().Evaluate(JsonNode.Parse($$"""
        {
          "Timeout": { "TotalSeconds": 10 },
          "TimeoutBehavior": {{(int)TimeoutBehavior.Wait}},
          "TimeoutBehaviorPolicy": {
            "OrchestrationAction": {{(int)OrchestrationActionOnTimeout.Continue}},
            "WaitingTime": { "Seconds": 5 }
          }
        }
        """)!.AsObject());

        Assert.Equal("Wait", policy.Behavior);
        Assert.Equal("Continue", policy.OrchestrationAction);
        Assert.Equal(TimeSpan.FromSeconds(5), policy.WaitingTime);
    }

    [Fact]
    public void Evaluate_ReadsReconcilePolicyAction()
    {
        var policy = new RuntimeTimeoutPolicyEvaluator().Evaluate(JsonNode.Parse($$"""
        {
          "Timeout": 15,
          "TimeoutBehavior": {{(int)TimeoutBehavior.Reconcile}},
          "TimeoutBehaviorPolicy": {
            "OrchestrationAction": {{(int)OrchestrationActionOnTimeout.Block}}
          }
        }
        """)!.AsObject());

        Assert.Equal("Reconcile", policy.Behavior);
        Assert.Equal("Block", policy.OrchestrationAction);
        Assert.Equal(TimeSpan.FromSeconds(15), policy.Timeout);
    }
}
