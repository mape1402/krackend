using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeBranchRuleEvaluatorTests
{
    [Fact]
    public void EvaluateStage_TakesFirstMatchingStageBranch()
    {
        var document = CreateDocument("true", ElementType.Stage, TargetStageId);
        var stage = document.Stages.First();

        var decision = CreateEvaluator().EvaluateStage(document, stage, JsonNode.Parse("""{"ok":true}"""));

        Assert.True(decision.HasRules);
        Assert.True(decision.IsTaken);
        Assert.True(decision.IsSupported);
        Assert.Equal("payment", decision.TargetStageKey);
        Assert.Equal(1, decision.TargetStageIndex);
    }

    [Fact]
    public void EvaluateStage_DoesNotTakeBranchWhenConditionIsFalse()
    {
        var document = CreateDocument("false", ElementType.Stage, TargetStageId);
        var stage = document.Stages.First();

        var decision = CreateEvaluator().EvaluateStage(document, stage, JsonNode.Parse("""{"ok":false}"""));

        Assert.True(decision.HasRules);
        Assert.False(decision.IsTaken);
        Assert.True(decision.IsSupported);
    }

    [Fact]
    public void EvaluateStage_MarksNonStageNavigationAsUnsupported()
    {
        var document = CreateDocument("true", ElementType.Task, TargetStageId);
        var stage = document.Stages.First();

        var decision = CreateEvaluator().EvaluateStage(document, stage, JsonNode.Parse("""{"ok":true}"""));

        Assert.True(decision.HasRules);
        Assert.False(decision.IsTaken);
        Assert.False(decision.IsSupported);
        Assert.Contains("stage-to-stage", decision.Reason);
    }

    private const string SourceStageId = "01KBRANCH000000000000000001";
    private const string TargetStageId = "01KBRANCH000000000000000002";

    private static RuntimeBranchRuleEvaluator CreateEvaluator()
        => new(new RuntimeConditionEvaluator());

    private static RuntimeArtifactDocument CreateDocument(string expression, ElementType navigateToType, string navigateToId)
        => RuntimeArtifactDocument.Parse(JsonNode.Parse($$"""
        {
          "Key": "order.fulfillment",
          "StageDefinitions": [
            {
              "Id": "{{SourceStageId}}",
              "Key": "reserve-inventory",
              "Order": 1,
              "BranchRules": [
                {
                  "Id": "01KBRANCH000000000000000010",
                  "FromType": {{(int)ElementType.Stage}},
                  "FromId": "{{SourceStageId}}",
                  "Condition": { "Configuration": { "Expression": { "Value": "{{expression}}" } } },
                  "NavigateToType": {{(int)navigateToType}},
                  "NavigateToId": "{{navigateToId}}"
                }
              ],
              "TaskDefinitions": []
            },
            {
              "Id": "{{TargetStageId}}",
              "Key": "payment",
              "Order": 2,
              "TaskDefinitions": []
            }
          ]
        }
        """)!);
}
